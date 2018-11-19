using Hspi.Exceptions;
using InfluxData.Net.Common.Constants;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx.Synchronous;
using NullGuard;
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
            this.tokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            influxDBClient = new InfluxDbClient(loginInformation.DBUri.ToString(),
                                                loginInformation.User,
                                                loginInformation.Password,
                                                InfluxDbVersion.v_1_3);
        }

        public InfluxDBLoginInformation LoginInformation => loginInformation;

        public bool IsTracked(int deviceRefId)
        {
            return peristenceDataMap?.ContainsKey(deviceRefId) ?? false;
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
                    var fields = new Dictionary<string, object>();

                    if (!string.IsNullOrWhiteSpace(value.Field))
                    {
                        double deviceValue = data.DeviceValue;

                        if (IsValidRange(value, deviceValue))
                        {
                            fields.Add(value.Field, data.DeviceValue);
                        }
                        else
                        {
                            Trace.TraceInformation(Invariant($"Not Recording Value for {data.Name} as there is no it does not have valid ranged value at {deviceValue}"));
                        }
                    }

                    AddIfNotEmpty(fields, value.FieldString, data.DeviceString);

                    if (fields.Count == 0)
                    {
                        Trace.TraceInformation(Invariant($"Not Recording Value for {data.Name} as there is no valid value to record."));
                        continue;
                    }

                    var tags = new Dictionary<string, object>()
                    {
                        {PluginConfig.DeviceNameTag, data.Name },
                    };

                    tags.Add(PluginConfig.DeviceRefIdTag, data.DeviceRefId);
                    AddIfNotEmpty(tags, PluginConfig.DeviceLocation1Tag, data.Location1);
                    AddIfNotEmpty(tags, PluginConfig.DeviceLocation2Tag, data.Location2);

                    foreach (var tag in value.Tags)
                    {
                        AddIfNotEmpty(tags, tag.Key, tag.Value);
                    }

                    var point = new Point()
                    {
                        Name = value.Measurement,
                        Fields = fields,
                        Tags = tags,
                        Timestamp = data.TimeStamp.ToUniversalTime(),
                    };

                    await queue.EnqueueAsync(point, tokenSource.Token).ConfigureAwait(false);
                }
            }

            return false;
        }

        public void Start(IEnumerable<DevicePersistenceData> persistenceData)
        {
            UpdatePeristenceData(persistenceData);

            sendPointsTask = Task.Factory.StartNew(SendPoints, tokenSource.Token,
                                                   TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                                   TaskScheduler.Current);
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

        private void AddIfNotEmpty(IDictionary<string, object> dict, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                dict.Add(key, value);
            }
        }

        private async Task SendPoints()
        {
            while (!tokenSource.Token.IsCancellationRequested)
            {
                List<Point> points = new List<Point>();
                points.Add(await queue.DequeueAsync(tokenSource.Token).ConfigureAwait(false));
                try
                {
                    await influxDBClient.Client.WriteAsync(points, loginInformation.DB, loginInformation.Retention, precision: TimeUnit.Seconds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"Failed to update {influxDBClient.Database} with {ExceptionHelper.GetFullMessage(ex)}"));
                    bool connected = await IsConnectedToServer().ConfigureAwait(false);

                    if (!connected)
                    {
                        foreach (var point in points)
                        {
                            await queue.EnqueueAsync(point, tokenSource.Token).ConfigureAwait(false);
                        }
                        await Task.Delay(30000, tokenSource.Token).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<bool> IsConnectedToServer()
        {
            try
            {
                var pong = await influxDBClient.Diagnostics.PingAsync().ConfigureAwait(false);
                return pong.Success;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            tokenSource.Cancel();
            sendPointsTask.WaitWithoutException();
        }

        private Task sendPointsTask;
        private readonly static AsyncProducerConsumerQueue<Point> queue = new AsyncProducerConsumerQueue<Point>();
        private readonly InfluxDbClient influxDBClient;
        private readonly InfluxDBLoginInformation loginInformation;
        private readonly CancellationTokenSource tokenSource;
        private volatile IReadOnlyDictionary<int, IReadOnlyList<DevicePersistenceData>> peristenceDataMap;
    }
}