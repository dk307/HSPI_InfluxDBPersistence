using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Pages
{
    public enum QueryDuration
    {
        [Description("1 hour")]
        D1h,

        [Description("6 hour")]
        D6h,

        [Description("12 hours")]
        D12h,

        [Description("24 hours")]
        D24h,

        [Description("7 days")]
        D7d,

        [Description("30 days")]
        D30d,

        [Description("60 days")]
        D60d,

        [Description("180 days")]
        D180d,

        [Description("365 days")]
        D365d,
    };

    internal static class InfluxDbQueryBuilder
    {
        public static async Task<string> CreateRegularTimeSeries(DevicePersistenceData data,
                                         QueryDuration queryDuration,
                                         InfluxDBLoginInformation loginInformation,
                                         TimeSpan groupByInterval,
                                         bool fileLinear = false)
        {
            string duration = GetInfluxDBDuration(queryDuration);

            // Find last element before duration
            string query = Invariant($"SELECT last(*) from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and time < now() - {duration} order by time asc");

            var time = await InfluxDBHelper.GetTimeValueForQuery(query, loginInformation).ConfigureAwait(false);

            string timeRestriction = time.HasValue ? Invariant($"time > {time.Value.ToUnixTimeSeconds()}s") : Invariant($"time > now() - {duration}");
            string fillOption = fileLinear ? "linear" : "previous";
            return Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" from \"{data.Measurement}\" WHERE \"{PluginConfig.DeviceRefIdTag}\" = '{data.DeviceRefId}' and {timeRestriction} GROUP BY time({(int)groupByInterval.TotalSeconds}s) fill({fillOption})");
        }

        public static string GetDeviceHistoryTabQuery(DevicePersistenceData data, string deviceName, QueryDuration duration)
        {
            var fields = string.Join(",", GetFields(data));

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(fields);
            stb.Append("AS \"");
            stb.Append(deviceName);
            stb.Append("\" from \"");
            stb.Append(data.Measurement);
            stb.Append("\" WHERE ");
            stb.Append(PluginConfig.DeviceRefIdTag);
            stb.Append("='");
            stb.AppendFormat(CultureInfo.InvariantCulture, "{0}", data.DeviceRefId);
            stb.Append("'");
            stb.AppendFormat(CultureInfo.InvariantCulture, "  AND time > now() - {0} ORDER BY time DESC", GetInfluxDBDuration(duration));

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

        public static async Task<string> GetStatsQuery(DevicePersistenceData data,
                                             QueryDuration queryDuration,
                                             InfluxDBLoginInformation loginInformation)
        {
            string subquery = await CreateRegularTimeSeries(data, queryDuration,
                                                            loginInformation, TimeSpan.FromSeconds(1));

            StringBuilder stb = new StringBuilder();
            stb.Append("SELECT ");
            stb.Append(Invariant($"MIN(\"{data.Field}\")"));
            stb.Append(Invariant($",MAX(\"{data.Field}\")"));
            stb.Append(Invariant($",MEAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MEDIAN(\"{data.Field}\")"));
            stb.Append(Invariant($",MODE(\"{data.Field}\")"));
            stb.Append(Invariant($",PERCENTILE(\"{data.Field}\", 95) as \"95 Percentile\""));
            stb.Append(Invariant($",STDDEV(\"{data.Field}\") as \"Standard Deviation\""));

            stb.AppendFormat(CultureInfo.InvariantCulture, "FROM ({0}) WHERE time > now() - {1} ", subquery, GetInfluxDBDuration(queryDuration));
            stb.AppendFormat(CultureInfo.InvariantCulture, "ORDER BY time DESC", subquery, GetInfluxDBDuration(queryDuration));

            //InfluxDBHelper.ExecuteInfluxDBQuery(stb.ToString(), loginInformation);
            return stb.ToString();
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