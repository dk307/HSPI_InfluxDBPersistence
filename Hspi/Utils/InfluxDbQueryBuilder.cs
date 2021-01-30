using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Utils
{
    internal static class InfluxDbQueryBuilder
    {
        public static async Task<string> GetHistogramQuery(DevicePersistenceData data,
                                                          TimeSpan queryDuration,
                                                          InfluxDBLoginInformation loginInformation)
        {
            DateTime? lastEntry;
            // Find last element before duration
            string query = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {queryDuration.TotalSeconds}s order by time asc");
            lastEntry = await InfluxDBHelper.GetTimeValueForQuery(query, loginInformation).ConfigureAwait(false);

            string timeRestriction = lastEntry.HasValue ? Invariant($"time >= {new DateTimeOffset(lastEntry.Value).ToUnixTimeSeconds()}s") : Invariant($"time >= now() - {queryDuration.TotalSeconds}s");
            return Invariant($"SELECT {GetFields(data)[0]} FROM \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' AND {timeRestriction} ORDER BY time ASC");
        }

        public static (string, string) GetHistoryQueries(DevicePersistenceData data,
                                                      string deviceName,
                                                      int? maxRecords,
                                                      TimeSpan? queryDuration)
        {
            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(GetFields(data)[0]);
            stb.Append(" AS \"");
            stb.Append(deviceName);
            stb.Append("\" from \"");
            stb.Append(data.Measurement);
            stb.Append("\" WHERE ");
            stb.Append(PluginConfig.DeviceRefIdTag);
            stb.Append("='");
            stb.AppendFormat(CultureInfo.InvariantCulture, "{0}", data.DeviceRefId);
            stb.Append('\'');
            if (queryDuration.HasValue)
            {
                stb.AppendFormat(CultureInfo.InvariantCulture, "  AND time > now() - {0}s", queryDuration.Value.TotalSeconds);
            }
            stb.Append("  ORDER BY time DESC");

            if (maxRecords.HasValue)
            {
                stb.AppendFormat(CultureInfo.InvariantCulture, "  LIMIT {0}", maxRecords.Value);
            }

            string lastValueQuery = Invariant($"SELECT {GetFields(data)[0]} AS \"{deviceName}\" from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' order by time desc Limit 1");
            return (stb.ToString(), lastValueQuery);
        }

        public static List<string> GetFields(DevicePersistenceData data)
        {
            List<string> fields = new List<string>();

            if (!string.IsNullOrWhiteSpace(data.Field))
            {
                fields.Add(Invariant($"\"{data.Field}\""));
            }
            else if (!string.IsNullOrWhiteSpace(data.FieldString))
            {
                fields.Add(Invariant($"\"{data.FieldString}\""));
            }

            return fields;
        }

        public static async Task<string> GetChartQuery(DevicePersistenceData data,
                                                                         string deviceName,
                                                                         TimeSpan queryDuration,
                                                                         InfluxDBLoginInformation loginInformation,
                                                                         TimeSpan? groupInterval,
                                                                         TimeSpan groupByOffset)
        {
            groupInterval = groupInterval ?? GetDefaultInfluxDBGroupInterval(queryDuration);

            string query = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {(queryDuration).TotalSeconds}s order by time asc");
            var time = await InfluxDBHelper.GetTimeValueForQuery(query, loginInformation).ConfigureAwait(false);

            string timeRestriction = time.HasValue ? Invariant($"time >= {new DateTimeOffset(time.Value).ToUnixTimeMilliseconds()}ms") : Invariant($"time >= now() - {(queryDuration).TotalSeconds}s");
            string fillOption = "previous";

            string subquery = Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and {timeRestriction} GROUP BY time({(int)groupInterval.Value.TotalSeconds}s, {groupByOffset.TotalSeconds}s) fill({fillOption})");

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"\"{data.Field}\" as \"{deviceName}\""));
            // We do not filter by time because filtering for time uses time of original entry, not filled ones
            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM (SELECT * FROM ({0})) LIMIT 100000", subquery);
            return stb.ToString();
        }

        public static async Task<IList<string>> GetStatsQueries(DevicePersistenceData data,
                                             TimeSpan queryDuration,
                                             InfluxDBLoginInformation loginInformation,
                                             TimeSpan? groupInterval,
                                             TimeSpan groupByOffset)
        {
            groupInterval = groupInterval ?? GetDefaultInfluxDBGroupInterval(queryDuration);

            string lastValueQuery = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {(queryDuration).TotalSeconds}s order by time asc");
            var lastEntry = await InfluxDBHelper.GetTimeValueForQuery(lastValueQuery, loginInformation).ConfigureAwait(false);

            string timeRestriction = lastEntry.HasValue ? Invariant($"time >= {new DateTimeOffset(lastEntry.Value).ToUnixTimeMilliseconds()}ms") : Invariant($"time >= now() - {(queryDuration).TotalSeconds}s");
            string minMaxQuery = Invariant($"SELECT MIN({data.Field}), MAX({data.Field}) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and {timeRestriction}");

            string subquery = Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and {timeRestriction} GROUP BY time({(int)groupInterval.Value.TotalSeconds}s, {groupByOffset.TotalSeconds}s) fill(previous)");
            var stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"MEAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MEDIAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MODE(\"{data.Field}\")"));
            stb.Append(Invariant($",PERCENTILE(\"{data.Field}\", 95) as \"95 Percentile\""));
            stb.Append(Invariant($",STDDEV(\"{data.Field}\") as \"Standard Deviation\""));
            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM (SELECT * FROM ({0}) WHERE time >= now() - {1}s)", subquery, queryDuration.TotalSeconds);

            return new List<string> { minMaxQuery, stb.ToString() };
        }

        private static TimeSpan GetDefaultInfluxDBGroupInterval(TimeSpan duration)
        {
            switch (duration)
            {
                case TimeSpan _ when duration <= TimeSpan.FromHours(1): return TimeSpan.FromSeconds(1);
                case TimeSpan _ when duration <= TimeSpan.FromHours(6): return TimeSpan.FromSeconds(10);
                case TimeSpan _ when duration <= TimeSpan.FromHours(12): return TimeSpan.FromSeconds(30);
                case TimeSpan _ when duration <= TimeSpan.FromHours(24): return TimeSpan.FromMinutes(1);
                case TimeSpan _ when duration <= TimeSpan.FromDays(7): return TimeSpan.FromMinutes(5);
                case TimeSpan _ when duration <= TimeSpan.FromDays(30): return TimeSpan.FromMinutes(60);
                case TimeSpan _ when duration <= TimeSpan.FromDays(60): return TimeSpan.FromHours(6);
                case TimeSpan _ when duration <= TimeSpan.FromDays(180): return TimeSpan.FromHours(12);
                default:
                    return TimeSpan.FromHours(24);
            }
        }
    }
}