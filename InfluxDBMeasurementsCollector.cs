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
                        Fields = new Dictionary<string, object>() { { value.Field, data.Data } },
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