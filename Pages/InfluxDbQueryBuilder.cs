using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Pages
{
    internal static class InfluxDbQueryBuilder
    {
        public static async Task<string> GetDeviceHistogramTabQuery(DevicePersistenceData data,
                                                          QueryDuration queryDuration,
                                                          InfluxDBLoginInformation loginInformation)
        {
            string duration = GetInfluxDBDuration(queryDuration);

            // Find last element before duration
            string query = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {duration} order by time asc");

            var time = await InfluxDBHelper.GetTimeValueForQuery(query, loginInformation).ConfigureAwait(false);

            string timeRestriction = time.HasValue ? Invariant($"time >= {new DateTimeOffset(time.Value).ToUnixTimeSeconds()}s") : Invariant($"time >= now() - {duration}");
            return Invariant($"SELECT {GetFields(data)[0]} FROM \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' AND {timeRestriction} ORDER BY time ASC");
        }

        public static string GetDeviceHistoryTabQuery(DevicePersistenceData data,
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

            return stb.ToString();
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

        public static async Task<string> GetGroupedDeviceHistoryTabQuery(DevicePersistenceData data,
                                                                         string deviceName,
                                                                         TimeSpan queryDuration,
                                                                         InfluxDBLoginInformation loginInformation,
                                                                         TimeSpan? groupInterval,
                                                                         TimeSpan groupByOffset)
        {
            groupInterval = groupInterval ?? GetDefaultInfluxDBGroupInterval(queryDuration);

            string subquery = await CreateRegularTimeSeries(data, queryDuration,
                                            loginInformation, groupInterval.Value, groupByOffset).ConfigureAwait(false);

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"\"{data.Field}\" as \"{deviceName}\""));
            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM (SELECT * FROM ({0}) WHERE time >= now() - {1}s)", subquery, queryDuration.TotalSeconds);
            return stb.ToString();
        }

        public static async Task<string> GetStatsQuery(DevicePersistenceData data,
                                             TimeSpan queryDuration,
                                             InfluxDBLoginInformation loginInformation,
                                             TimeSpan? groupInterval,
                                             TimeSpan groupByOffset)
        {
            groupInterval = groupInterval ?? GetDefaultInfluxDBGroupInterval(queryDuration);
            string subquery = await CreateRegularTimeSeries(data, queryDuration,
                                                            loginInformation, groupInterval.Value, groupByOffset).ConfigureAwait(false);

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"MIN(\"{data.Field}\")"));
            stb.Append(Invariant($",MAX(\"{data.Field}\")"));
            stb.Append(Invariant($",MEAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MEDIAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MODE(\"{data.Field}\")"));
            stb.Append(Invariant($",PERCENTILE(\"{data.Field}\", 95) as \"95 Percentile\""));
            stb.Append(Invariant($",STDDEV(\"{data.Field}\") as \"Standard Deviation\""));

            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM (SELECT * FROM ({0}) WHERE time >= now() - {1}s)", subquery, queryDuration.TotalSeconds);
            stb.Append(" LIMIT 100000");

            return stb.ToString();
        }

        private static async Task<string> CreateRegularTimeSeries(DevicePersistenceData data,
                                                                  TimeSpan queryDuration,
                                                                  InfluxDBLoginInformation loginInformation,
                                                                  TimeSpan groupByInterval,
                                                                  TimeSpan groupByOffset,
                                                                  bool fileLinear = false)
        {
            // Find last element before duration
            string query = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {queryDuration.TotalSeconds}s order by time asc");

            var time = await InfluxDBHelper.GetTimeValueForQuery(query, loginInformation).ConfigureAwait(false);

            string timeRestriction = time.HasValue ? Invariant($"time >= {new DateTimeOffset(time.Value).ToUnixTimeSeconds()}s") : Invariant($"time >= now() - {queryDuration.TotalSeconds}s");
            string fillOption = fileLinear ? "linear" : "previous";
            return Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and {timeRestriction} GROUP BY time({(int)groupByInterval.TotalSeconds}s, {groupByOffset.TotalSeconds}s) fill({fillOption})");
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

        private static string GetInfluxDBDuration(QueryDuration duration)
        {
            switch (duration)
            {
                case QueryDuration.D1h: return "1h";
                case QueryDuration.D6h: return "6h";
                case QueryDuration.D12h: return "12h";
                case QueryDuration.D24h: return "24h";
                case QueryDuration.D7d: return "7d";
                case QueryDuration.D30d: return "30d";
                case QueryDuration.D60d: return "60d";
                case QueryDuration.D180d: return "180d";
                case QueryDuration.D365d: return "365d";
                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
            }
        }
    }
}