using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;

namespace InfluxData.Net.InfluxDb.ResponseParsers
{
    public interface IDatabaseResponseParser
    {
        IEnumerable<Database> GetDatabases(IEnumerable<Serie> series);
    }
}