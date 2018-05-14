using System;

namespace Hspi
{
    internal class ImportDeviceData
    {
        public ImportDeviceData(string id, string name, string sql, in TimeSpan interval)
        {
            Id = id;
            Name = name;
            Sql = sql;
            Interval = interval;
        }

        public string Id { get; }
        public string Name { get; }
        public string Sql { get; }
        public TimeSpan Interval { get; }
    }
}