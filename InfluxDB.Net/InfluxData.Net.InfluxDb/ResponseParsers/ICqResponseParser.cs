using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;

namespace InfluxData.Net.InfluxDb.ResponseParsers
{
    public interface ICqResponseParser
    {
        IEnumerable<ContinuousQuery> GetContinuousQueries(string dbName, IEnumerable<Serie> series);
    }
}