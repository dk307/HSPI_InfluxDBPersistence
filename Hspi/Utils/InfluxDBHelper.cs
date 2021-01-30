using AdysTech.InfluxDB.Client.Net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hspi.Utils
{
    internal static class InfluxDBHelper
    {
        public static IDictionary<string, TimeSpan> CreateHistogram(IList<IDictionary<string, object>> queryData,
                                                             TimeSpan durationTimeSpan)
        {
            DateTime utcNow = DateTime.UtcNow;
            var lowerClip = utcNow - durationTimeSpan;

            var histogram = new Dictionary<string, TimeSpan>();

            // data is ascending
            DateTime? previousDateTime = null;
            string? previousValue = null;
            foreach (var row in queryData)
            {
                var dateTime = (DateTime)row[InfluxDBHelper.TimeColumn];
                var rowValue = GetSerieValue(CultureInfo.InvariantCulture, row.FirstOrDefault(x => x.Key != InfluxDBHelper.TimeColumn).Value);

                if (dateTime >= lowerClip)
                {
                    if ((previousDateTime != null) && (previousValue != null))
                    {
                        AddTimespanToHistogram(histogram, dateTime - previousDateTime.Value, previousValue);
                    }
                }

                previousDateTime = dateTime < lowerClip ? lowerClip : dateTime;
                previousValue = rowValue;
            }

            if ((previousDateTime.HasValue) && (previousValue != null))
            {
                AddTimespanToHistogram(histogram, utcNow - previousDateTime.Value, previousValue);
            }

            return histogram;

            static void AddTimespanToHistogram(IDictionary<string, TimeSpan> value,
                                         TimeSpan timeSpan,
                                         string previousValue)
            {
                TimeSpan existingValue = new TimeSpan();
                _ = value.TryGetValue(previousValue, out existingValue);
                value[previousValue] = existingValue.Add(timeSpan);
            }
        }

        public static async Task<IList<IDictionary<string, object>>> ExecuteInfluxDBQuery(string query, InfluxDBLoginInformation loginInformation)
        {
            var accumatedList = new List<IDictionary<string, object>>();
            if (loginInformation.DBUri != null)
            {
                using var influxDbClient = new InfluxDBClient(loginInformation.DBUri.ToString(), loginInformation.User, loginInformation.Password);

                var series = await influxDbClient.QueryMultiSeriesAsync(loginInformation.DB, query, precision: TimePrecision.Seconds).ConfigureAwait(false);

                foreach (var serie in series)
                {
                    accumatedList.AddRange(serie.Entries.Select(x => (IDictionary<string, object>)x));
                }
            }
            return accumatedList;
        }

        public static string? GetSerieValue(CultureInfo culture, object? column)
        {
            switch (column)
            {
                case double doubleValue:
                    return RoundDoubleValue(culture, doubleValue);

                case float floatValue:
                    return RoundDoubleValue(culture, floatValue);

                case TimeSpan span:
                    {
                        StringBuilder stringBuilder = new StringBuilder();

                        int days = span.Days;
                        if (days > 0)
                        {
                            stringBuilder.AppendFormat(culture, "{0} {1}", days, (days > 1 ? "days" : "day"));
                        }

                        int hours = span.Hours;
                        if (hours > 0)
                        {
                            if (stringBuilder.Length > 1)
                            {
                                stringBuilder.Append(' ');
                            }
                            stringBuilder.AppendFormat(culture, "{0} {1}", hours, (hours > 1 ? "hours" : "hour"));
                        }
                        int minutes = span.Minutes;
                        if (minutes > 0)
                        {
                            if (stringBuilder.Length > 1)
                            {
                                stringBuilder.Append(' ');
                            }
                            stringBuilder.AppendFormat(culture, "{0} {1}", minutes, (minutes > 1 ? "minutes" : "minute"));
                        }

                        int seconds = span.Seconds;
                        if (seconds > 0)
                        {
                            if (stringBuilder.Length > 1)
                            {
                                stringBuilder.Append(' ');
                            }
                            stringBuilder.AppendFormat(culture, "{0} {1}", seconds, (seconds > 1 ? "seconds" : "second"));
                        }

                        return stringBuilder.ToString();
                    }

                case null:
                    return null;

                case string stringValue:
                    if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedValue))
                    {
                        return RoundDoubleValue(culture, parsedValue);
                    }
                    return stringValue;

                default:
                    return Convert.ToString(column, culture);
            }

            static string RoundDoubleValue(CultureInfo culture, double floatValue)
            {
                return Math.Round(floatValue, 3, MidpointRounding.AwayFromZero).ToString("G", culture);
            }
        }

        public static async Task<object?> GetSingleValueForQuery(string query, InfluxDBLoginInformation loginInformation)
        {
            var queryData = await ExecuteInfluxDBQuery(query, loginInformation).ConfigureAwait(false);
            if (queryData.Count > 0)
            {
                var firstRow = queryData[0];
                return firstRow.FirstOrDefault(x => x.Key != TimeColumn).Value;
            }

            return null;
        }

        public static async Task<DateTime?> GetTimeValueForQuery(string query, InfluxDBLoginInformation loginInformation)
        {
            var queryData = await ExecuteInfluxDBQuery(query, loginInformation).ConfigureAwait(false);
            if (queryData.Count > 0)
            {
                return (DateTime)queryData[0][TimeColumn];
            }

            return null;
        }

        public const string TimeColumn = "Time";
    }
}