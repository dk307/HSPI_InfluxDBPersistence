using AdysTech.InfluxDB.Client.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hspi.Utils
{
    internal static class InfluxDBHelper
    {
        public const string TimeColumn = "Time";

        public static async Task<IList<IDictionary<string, object>>> ExecuteInfluxDBQuery(string query, InfluxDBLoginInformation loginInformation)
        {
            using (var influxDbClient = new InfluxDBClient(loginInformation.DBUri.ToString(), loginInformation.User, loginInformation.Password))
            {
                var series = await influxDbClient.QueryMultiSeriesAsync(loginInformation.DB, query, precision: TimePrecision.Seconds).ConfigureAwait(false);

                var accumatedList = new List<IDictionary<string, object>>();

                foreach (var serie in series)
                {
                    accumatedList.AddRange(serie.Entries.Select(x => (IDictionary<string, object>)x));
                }
                return accumatedList;
            }
        }

        public static async Task<object> GetSingleValueForQuery(string query, InfluxDBLoginInformation loginInformation)
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
    }
}