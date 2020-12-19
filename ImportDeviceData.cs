using NullGuard;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class ImportDeviceData
    {
        public ImportDeviceData(string id, string sql, in long intervalSeconds, [AllowNull] string unit)
        {
            Id = id;
            Sql = sql;
            IntervalSeconds = intervalSeconds;
            Unit = unit;
        }

        public string Id { get; }

        public long IntervalSeconds { get; }
        public string Sql { get; }
        public string Unit { get; }

#pragma warning disable CA1822 // Mark members as static
        public int Version => 1;
#pragma warning restore CA1822 // Mark members as static
    }
}