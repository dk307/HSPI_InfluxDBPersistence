using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.ClientSubModules;
using InfluxData.Net.InfluxDb.Models;
using System;
using System.Collections.Generic;

namespace Hspi
{
    internal partial class InfluxDBMeasurementsCollector
    {

        public bool Record(RecordData data)
        {
            if (persistenceData.TryGetValue(data.DeviceRefId, out var value))
            {
                var point = new Point()
                {
                    Name = value.Measurement,
                    Fields = new Dictionary<string, object>() { { value.Field, data.Data } },
                };
                batchWriter.AddPoint(point);
            }

            return false;
        }

        private readonly InfluxDbClient influxDBClient;
        private readonly BatchWriter batchWriter;

        private readonly InfluxDBLoginInformation loginInformation;
        private readonly IReadOnlyDictionary<int, DevicePersistenceData> persistenceData;
    }
}