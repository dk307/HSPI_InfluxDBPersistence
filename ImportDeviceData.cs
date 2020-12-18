using System;

namespace Hspi
{
    internal sealed class ImportDeviceData
    {
        public ImportDeviceData(string id, string name, string sql, in TimeSpan interval, string unit)
        {
            Id = id;
            Name = name;
            Sql = sql;
            Interval = interval;
            Unit = unit;
        }

        public string Id { get; }
        public string Name { get; }
        public string Sql { get; }
        public TimeSpan Interval { get; }
        public string Unit { get; }
    }
}