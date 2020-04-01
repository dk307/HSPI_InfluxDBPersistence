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
                                                                  QueryDuration queryDuration)
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
            stb.Append("'");
            stb.AppendFormat(CultureInfo.InvariantCulture, "  AND time > now() - {0} ORDER BY time DESC", GetInfluxDBDuration(queryDuration));

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
                                                                         QueryDuration queryDuration,
                                                                         InfluxDBLoginInformation loginInformation,
                                                                         TimeSpan? groupInterval)
        {
            groupInterval = groupInterval ?? GetDefaultInfluxDBGroupInterval(queryDuration);

            string subquery = await CreateRegularTimeSeries(data, queryDuration,
                                            loginInformation, groupInterval.Value).ConfigureAwait(false);

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"\"{data.Field}\" as \"{deviceName}\""));

            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM (SELECT * FROM ({0}) WHERE time >= now() - {1})", subquery, GetInfluxDBDuration(queryDuration));

            return stb.ToString();
        }

        public static async Task<string> GetStatsQuery(DevicePersistenceData data,
                                             QueryDuration queryDuration,
                                             InfluxDBLoginInformation loginInformation,
                                             TimeSpan? groupInterval)
        {
            groupInterval = groupInterval ?? GetDefaultInfluxDBGroupInterval(queryDuration);
            string subquery = await CreateRegularTimeSeries(data, queryDuration,
                                                            loginInformation, groupInterval.Value).ConfigureAwait(false);

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"MIN(\"{data.Field}\")"));
            stb.Append(Invariant($",MAX(\"{data.Field}\")"));
            stb.Append(Invariant($",MEAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MEDIAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MODE(\"{data.Field}\")"));
            stb.Append(Invariant($",PERCENTILE(\"{data.Field}\", 95) as \"95 Percentile\""));
            stb.Append(Invariant($",STDDEV(\"{data.Field}\") as \"Standard Deviation\""));

            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM (SELECT * FROM ({0}) WHERE time >= now() - {1})", subquery, GetInfluxDBDuration(queryDuration));
            stb.Append(" LIMIT 100000");

            return stb.ToString();
        }

        public static TimeSpan GetTimeSpan(QueryDuration duration)
        {
            switch (duration)
            {
                case QueryDuration.D1h: return TimeSpan.FromHours(1);
                case QueryDuration.D6h: return TimeSpan.FromHours(6);
                case QueryDuration.D12h: return TimeSpan.FromHours(12);
                case QueryDuration.D24h: return TimeSpan.FromHours(24);
                case QueryDuration.D7d: return TimeSpan.FromHours(24 *7 );
                case QueryDuration.D30d: return TimeSpan.FromHours(30 * 24);
                case QueryDuration.D60d: return TimeSpan.FromHours(60 * 24 );
                case QueryDuration.D180d: return TimeSpan.FromHours(180 * 24);
                case QueryDuration.D365d: return TimeSpan.FromHours(365 * 24);
                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
            }
        }

        private static async Task<string> CreateRegularTimeSeries(DevicePersistenceData data,
                                                                         QueryDuration queryDuration,
                                         InfluxDBLoginInformation loginInformation,
                                         TimeSpan groupByInterval,
                                         bool fileLinear = false)
        {
            string duration = GetInfluxDBDuration(queryDuration);

            // Find last element before duration
            string query = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {duration} order by time asc");

            var time = await InfluxDBHelper.GetTimeValueForQuery(query, loginInformation).ConfigureAwait(false);

            string timeRestriction = time.HasValue ? Invariant($"time >= {new DateTimeOffset(time.Value).ToUnixTimeSeconds()}s") : Invariant($"time >= now() - {duration}");
            string fillOption = fileLinear ? "linear" : "previous";
            return Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and {timeRestriction} GROUP BY time({(int)groupByInterval.TotalSeconds}s) fill({fillOption})");
        }

        private static TimeSpan GetDefaultInfluxDBGroupInterval(QueryDuration duration)
        {
            switch (duration)
            {
                case QueryDuration.D1h: return TimeSpan.FromSeconds(1);
                case QueryDuration.D6h: return TimeSpan.FromSeconds(5);
                case QueryDuration.D12h: return TimeSpan.FromSeconds(15);
                case QueryDuration.D24h: return TimeSpan.FromMinutes(1);
                case QueryDuration.D7d: return TimeSpan.FromMinutes(5);
                case QueryDuration.D30d: return TimeSpan.FromMinutes(30);
                case QueryDuration.D60d: return TimeSpan.FromHours(1);
                case QueryDuration.D180d: return TimeSpan.FromHours(12);
                case QueryDuration.D365d: return TimeSpan.FromHours(24);
                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
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