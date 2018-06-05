using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;

namespace InfluxData.Net.InfluxDb.ResponseParsers
{
    public interface IRetentionResponseParser
    {
        IEnumerable<RetentionPolicy> GetRetentionPolicies(string dbName, IEnumerable<Serie> series);
    }
}