using Hspi.Exceptions;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
    using static System.FormattableString;

    internal class InfluxDBMeasurementsCollector
    {
        public InfluxDBMeasurementsCollector(InfluxDBLoginInformation loginInformation)
        {
            this.loginInformation = loginInformation;
            influxDBClient = new InfluxDbClient(loginInformation.DBUri.ToString(),
                                                loginInformation.User,
                                                loginInformation.Password,
                                                InfluxDbVersion.v_1_3);
        }

        public InfluxDBLoginInformation LoginInformation => loginInformation;

        public bool IsTracked(int deviceRefId)
        {
            Interlocked.MemoryBarrier();
            return peristenceDataMap.ContainsKey(deviceRefId);
        }

        public async Task<bool> Record(RecordData data)
        {
            Interlocked.MemoryBarrier();
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

                    if (!string.IsNullOrWhiteSpace(value.FieldString))
                    {
                        fields.Add(value.FieldString, data.DeviceString);
                    }

                    if (fields.Count == 0)
                    {
                        Trace.TraceInformation(Invariant($"Not Recording Value for {data.Name} as there is no valid value to record."));
                        continue;
                    }

                    var tags = new Dictionary<string, object>()
                    {
                        {"name", data.Name },
                        {"location1", data.Location1 },
                        {"location2", data.Location2 },
                    };

                    foreach (var tag in value.Tags)
                    {
                        tags.Add(tag.Key, tag.Value);
                    }

                    var point = new Point()
                    {
                        Name = value.Measurement,
                        Fields = fields,
                        Tags = tags,
                    };

                    await queue.EnqueueAsync(point).ConfigureAwait(false);
                }
            }

            return false;
        }

        public void Start(IEnumerable<DevicePersistenceData> persistenceData,
                          CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            UpdatePeristenceData(persistenceData);

            Task.Factory.StartNew(SendPoints, cancellationToken,
                        TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Current);
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

            Interlocked.Exchange(ref peristenceDataMap,
                                 map.ToDictionary(x => x.Key, x => x.Value as IReadOnlyList<DevicePersistenceData>));     //atomic swap
        }

        private static bool IsValidRange(DevicePersistenceData value, double deviceValue)
        {
            double maxValidValue = value.MaxValidValue ?? double.MaxValue;
            double minValidValue = value.MinValidValue ?? double.MinValue;
            return !double.IsNaN(deviceValue) && (deviceValue <= maxValidValue) && (deviceValue >= minValidValue);
        }

        private async Task SendPoints()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var point = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await influxDBClient.Client.WriteAsync(point, loginInformation.DB, precision: "s").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"Failed to update {influxDBClient.Database} with {ExceptionHelper.GetFullMessage(ex)}"));
                }
            }
        }

        private readonly static AsyncProducerConsumerQueue<Point> queue = new AsyncProducerConsumerQueue<Point>();
        private readonly InfluxDbClient influxDBClient;
        private readonly InfluxDBLoginInformation loginInformation;
        private CancellationToken cancellationToken;
        private IReadOnlyDictionary<int, IReadOnlyList<DevicePersistenceData>> peristenceDataMap;
    }
}