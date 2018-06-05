using InfluxData.Net.InfluxDb.Models.Responses;
using System.Collections.Generic;

namespace InfluxData.Net.InfluxDb.ResponseParsers
{
    public interface IBasicResponseParser
    {
        IEnumerable<Serie> FlattenResultsSeries(IEnumerable<SeriesResult> seriesResults);

        IEnumerable<IEnumerable<Serie>> MapResultsSeries(IEnumerable<SeriesResult> seriesResults);
    }
}