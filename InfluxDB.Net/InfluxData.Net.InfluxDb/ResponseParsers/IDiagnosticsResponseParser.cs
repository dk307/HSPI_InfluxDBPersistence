using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;

namespace InfluxData.Net.InfluxDb.ResponseParsers
{
    public interface IDiagnosticsResponseParser
    {
        Stats GetStats(IEnumerable<Serie> series);

        Diagnostics GetDiagnostics(IEnumerable<Serie> series);
    }
}