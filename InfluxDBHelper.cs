using InfluxData.Net.Common.Constants;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hspi
{
    internal static class InfluxDBHelper
    {
        public static async Task<IEnumerable<Serie>> ExecuteInfluxDBQuery(string query, InfluxDBLoginInformation loginInformation)
        {
            var influxDbClient = new InfluxDbClient(loginInformation.DBUri.ToString(), loginInformation.User, loginInformation.Password, InfluxDbVersion.v_1_3);
            return await influxDbClient.Client.QueryAsync(query, loginInformation.DB, TimeUnit.Seconds).ConfigureAwait(false);
        }

        public static async Task<object> GetSingleValueForQuery(string query, InfluxDBLoginInformation loginInformation)
        {
            var queryData = (await ExecuteInfluxDBQuery(query, loginInformation).ConfigureAwait(false)).FirstOrDefault();

            if (queryData != null)
            {
                if (queryData.Values.Count > 0)
                {
                    // first row, second column
                    if (queryData.Values[0].Count > 1)
                    {
                        return queryData.Values[0][1];
                    }
                }
            }

            return null;
        }
    }
}