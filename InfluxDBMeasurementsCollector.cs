using AdysTech.InfluxDB.Client.Net;
using Hspi.Exceptions;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class InfluxDBMeasurementsCollector : IDisposable
    {
        public InfluxDBMeasurementsCollector(InfluxDBLoginInformation loginInformation,
                                             CancellationToken shutdownToken)
        {
            this.loginInformation = loginInformation;
            tokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            influxDBClient = new InfluxDBClient(loginInformation.DBUri.ToString(),
                                                loginInformation.User,
                                                loginInformation.Password);
        }

        public InfluxDBLoginInformation LoginInformation => loginInformation;

        public bool IsTracked(int deviceRefId, TrackedType? trackedType)
        {
            if (peristenceDataMap != null)
            {
                if (peristenceDataMap.TryGetValue(deviceRefId, out var devicePersistenceDatas))
                {
                    if (!trackedType.HasValue)
                    {
                        return devicePersistenceDatas.Count > 0;
                    }
                    else
                    {
                        return devicePersistenceDatas.Any(x => x.TrackedType == trackedType.Value);
                    }
                }
            }

            return false;
        }

        public async Task<bool> Record(RecordData data)
        {
            if (peristenceDataMap == null)
            {
                throw new HspiException("Collection not started");
            }
            if (peristenceDataMap.TryGetValue(data.DeviceRefId, out var peristenceData))
            {
                foreach (var value in peristenceData)
                {
                    var influxDatapoint = new InfluxDatapoint<InfluxValueField>()
                    {
                        MeasurementName = value.Measurement,
                        Precision = TimePrecision.Seconds,
                        UtcTimestamp = data.TimeStamp.ToUniversalTime(),
                    };

                    if (!string.IsNullOrWhiteSpace(value.Field))
                    {
                        double deviceValue = data.DeviceValue;

                        if (IsValidRange(value, deviceValue))
                        {
                            influxDatapoint.Fields.Add(value.Field, new InfluxValueField(deviceValue));
                        }
                        else
                        {
                            Trace.TraceInformation(Invariant($"Not Recording Value for {data.Name} as there is no it does not have valid ranged value at {deviceValue}"));
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(value.FieldString))
                    {
                        influxDatapoint.Fields.Add(value.FieldString, new InfluxValueField(data.DeviceString));
                    }

                    if (influxDatapoint.Fields.Count == 0)
                    {
                        Trace.TraceInformation(Invariant($"Not Recording Value for {data.Name} as there is no valid value to record."));
                        continue;
                    }

                    influxDatapoint.Tags.Add(PluginConfig.DeviceNameTag, data.Name);
                    influxDatapoint.Tags.Add(PluginConfig.DeviceRefIdTag, Convert.ToString(data.DeviceRefId, CultureInfo.InvariantCulture));

                    AddIfNotEmpty(influxDatapoint.Tags, PluginConfig.DeviceLocation1Tag, data.Location1);
                    AddIfNotEmpty(influxDatapoint.Tags, PluginConfig.DeviceLocation2Tag, data.Location2);

                    foreach (var tag in value.Tags)
                    {
                        AddIfNotEmpty(influxDatapoint.Tags, tag.Key, tag.Value);
                    }

                    await queue.EnqueueAsync(influxDatapoint, tokenSource.Token).ConfigureAwait(false);
                }
            }

            return false;
        }

        public void Start(IEnumerable<DevicePersistenceData> persistenceData)
        {
            UpdatePeristenceData(persistenceData);
            Utils.TaskHelper.StartAsyncWithErrorChecking("DB Send Points", SendPoints, tokenSource.Token);
        }

        public void UpdatePeristenceData(IEnumerable<DevicePersistenceData> persistenceData)
        {
            var map = new Dictionary<int, List<DevicePersistenceData>>();

            foreach (var data in persistenceData)
            {
                if (!map.ContainsKey(data.DeviceRefId))
                {
                    map.Add(data.DeviceRefId, new List<DevicePersistenceData>());
                }
                map[data.DeviceRefId].Add(data);
            }

            peristenceDataMap = map.ToDictionary(x => x.Key, x => x.Value as IReadOnlyList<DevicePersistenceData>);
        }

        private static bool IsValidRange(DevicePersistenceData value, double deviceValue)
        {
            double maxValidValue = value.MaxValidValue ?? double.MaxValue;
            double minValidValue = value.MinValidValue ?? double.MinValue;
            return !double.IsNaN(deviceValue) && (deviceValue <= maxValidValue) && (deviceValue >= minValidValue);
        }

        private static void AddIfNotEmpty(IDictionary<string, string> dict, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                dict.Add(key, value);
            }
        }

        private async Task SendPoints()
        {
            CancellationToken token = tokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                var point = await queue.DequeueAsync(token).ConfigureAwait(false);

                try
                {
                    if (!await influxDBClient.PostPointAsync(loginInformation.DB, point).ConfigureAwait(false))
                    {
                        Trace.TraceWarning(Invariant($"Failed to update {loginInformation.DB} for {point.ToString()}"));
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    Trace.TraceWarning(Invariant($"Failed to update {loginInformation.DB} with {ExceptionHelper.GetFullMessage(ex)} for {point.ToString()}"));
                    bool connected = await IsConnectedToServer().ConfigureAwait(false);

                    if (!connected)
                    {
                        Trace.TraceWarning(Invariant($"DB is down. Waiting for {connectFailureDelay} before sending message"));
                        await queue.EnqueueAsync(point, token).ConfigureAwait(false);
                        await Task.Delay(connectFailureDelay, token).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<bool> IsConnectedToServer()
        {
            try
            {
                await influxDBClient.GetServerVersionAsync().ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            influxDBClient?.Dispose();
            tokenSource.Cancel();
        }

        private static readonly AsyncProducerConsumerQueue<InfluxDatapoint<InfluxValueField>> queue
            = new AsyncProducerConsumerQueue<InfluxDatapoint<InfluxValueField>>();

        private readonly InfluxDBClient influxDBClient;
        private readonly InfluxDBLoginInformation loginInformation;
        private readonly CancellationTokenSource tokenSource;
        private volatile IReadOnlyDictionary<int, IReadOnlyList<DevicePersistenceData>> peristenceDataMap;
        private static readonly TimeSpan connectFailureDelay = TimeSpan.FromSeconds(30);
    }
}