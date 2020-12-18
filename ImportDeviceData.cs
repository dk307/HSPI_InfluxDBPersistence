using NullGuard;
using System;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class ImportDeviceData
    {
        public ImportDeviceData(string id, string name, string sql, in TimeSpan interval, [AllowNull] string unit)
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

#pragma warning disable CA1822 // Mark members as static
        public int Version => 1;
#pragma warning restore CA1822 // Mark members as static
    }
}