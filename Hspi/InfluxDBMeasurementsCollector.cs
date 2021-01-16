using AdysTech.InfluxDB.Client.Net;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
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
                                             PluginStatusCalculator pluginStatusCalculator,
                                             CancellationToken shutdownToken)
        {
            this.loginInformation = loginInformation;
            this.pluginStatusCalculator = pluginStatusCalculator;
            tokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            influxDBClient = new InfluxDBClient(loginInformation.DBUri.ToString(),
                                                loginInformation.User,
                                                loginInformation.Password);
        }

        public InfluxDBLoginInformation LoginInformation => loginInformation;

        public void Dispose()
        {
            influxDBClient?.Dispose();
            tokenSource.Cancel();
        }

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
                throw new ArgumentException("Collection not started");
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
                            logger.Info(Invariant($"Not Recording Value for {data.Name} as there is no it does not have valid ranged value at {deviceValue}"));
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(value.FieldString))
                    {
                        influxDatapoint.Fields.Add(value.FieldString, new InfluxValueField(data.DeviceString));
                    }

                    if (influxDatapoint.Fields.Count == 0)
                    {
                        logger.Info(Invariant($"Not Recording Value for {data.Name} as there is no valid value to record."));
                        continue;
                    }

                    influxDatapoint.Tags.Add(PluginConfig.DeviceNameTag, data.Name);
                    influxDatapoint.Tags.Add(PluginConfig.DeviceRefIdTag, Convert.ToString(data.DeviceRefId, CultureInfo.InvariantCulture));

                    AddIfNotEmpty(influxDatapoint.Tags, PluginConfig.DeviceLocation1Tag, data.Location1);
                    AddIfNotEmpty(influxDatapoint.Tags, PluginConfig.DeviceLocation2Tag, data.Location2);

                    if (value.Tags != null)
                    {
                        foreach (var tag in value.Tags)
                        {
                            AddIfNotEmpty(influxDatapoint.Tags, tag.Key, tag.Value);
                        }
                    }

                    var queueElement = new QueueElement()
                    {
                        datapoint = influxDatapoint,
                        refId = data.DeviceRefId,
                    };

                    await queue.EnqueueAsync(queueElement, tokenSource.Token).ConfigureAwait(false);
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

        private static void AddIfNotEmpty(IDictionary<string, string> dict, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                dict.Add(key, value);
            }
        }

        private static bool IsValidRange(DevicePersistenceData value, double deviceValue)
        {
            double maxValidValue = value.MaxValidValue ?? double.MaxValue;
            double minValidValue = value.MinValidValue ?? double.MinValue;
            return !double.IsNaN(deviceValue) && (deviceValue <= maxValidValue) && (deviceValue >= minValidValue);
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

        private async Task SendPoints()
        {
            CancellationToken token = tokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                var queueElement = await queue.DequeueAsync(token).ConfigureAwait(false);

                try
                {
                    if (!await influxDBClient.PostPointAsync(loginInformation.DB, queueElement.datapoint).ConfigureAwait(false))
                    {
                        logger.Warn(Invariant($"Failed to update {loginInformation.DB} for RefId: {queueElement.refId}"));
                    }
                    else
                    {
                        await pluginStatusCalculator.ExportWorked(token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    await pluginStatusCalculator.ExportErrored(token).ConfigureAwait(false);

                    bool connected = await IsConnectedToServer().ConfigureAwait(false);
                    if (!connected)
                    {
                        logger.Warn(Invariant($"{loginInformation?.DBUri.ToString() ?? "DB"} is not connectable. Waiting for {connectFailureDelay.TotalSeconds} seconds before sending messages again"));
                        await queue.EnqueueAsync(queueElement, token).ConfigureAwait(false);
                        await Task.Delay(connectFailureDelay, token).ConfigureAwait(false);
                    }
                    else
                    {
                        logger.Warn(Invariant($"Failed to update {loginInformation.DB} with {ExceptionHelper.GetFullMessage(ex)} for RefId: {queueElement.refId}"));
                    }
                }
            }
        }

        private struct QueueElement
        {
            public InfluxDatapoint<InfluxValueField> datapoint;
            public int refId;
        };

        private static readonly TimeSpan connectFailureDelay = TimeSpan.FromSeconds(30);
        private static readonly AsyncProducerConsumerQueue<QueueElement> queue = new AsyncProducerConsumerQueue<QueueElement>();
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly InfluxDBClient influxDBClient;
        private readonly InfluxDBLoginInformation loginInformation;
        private readonly PluginStatusCalculator pluginStatusCalculator;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly CancellationTokenSource tokenSource;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private volatile IReadOnlyDictionary<int, IReadOnlyList<DevicePersistenceData>> peristenceDataMap;
    }
}