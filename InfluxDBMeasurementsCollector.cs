using Hspi.Exceptions;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.ClientSubModules;
using InfluxData.Net.InfluxDb.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hspi
{
    internal class InfluxDBMeasurementsCollector
    {
        public InfluxDBMeasurementsCollector(InfluxDBLoginInformation loginInformation)
        {
            this.loginInformation = loginInformation;
            influxDBClient = new InfluxDbClient(loginInformation.DBUri.ToString(),
                                                loginInformation.User,
                                                loginInformation.Password,
                                                InfluxDbVersion.v_1_3);

            batchWriter = influxDBClient.Serie.CreateBatchWriter(loginInformation.DB, precision: "s");
        }

        public InfluxDBLoginInformation LoginInformation => loginInformation;

        public bool IsTracked(int deviceRefId)
        {
            Interlocked.MemoryBarrier();
            return peristenceDataMap.ContainsKey(deviceRefId);
        }

        public bool Record(RecordData data)
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
                    batchWriter.AddPoint(point);
                }
            }

            return false;
        }

        public void Start(IEnumerable<DevicePersistenceData> persistenceData,
                          CancellationToken cancellationToken)
        {
            UpdatePeristenceData(persistenceData);

            batchWriter.Start(1000, continueOnError: true);
            cancellationToken.Register(() => { batchWriter.Stop(); });
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

        public void Stop()
        {
            batchWriter?.Stop();
        }

        private readonly InfluxDbClient influxDBClient;
        private readonly InfluxDBLoginInformation loginInformation;
        private IBatchWriter batchWriter;
        private IReadOnlyDictionary<int, IReadOnlyList<DevicePersistenceData>> peristenceDataMap;
    }
}