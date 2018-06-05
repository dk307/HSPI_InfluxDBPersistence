using InfluxData.Net.Common.Infrastructure;
using Newtonsoft.Json;

namespace InfluxData.Net.InfluxData.Helpers
{
    public static class ResponseExtensionsBase
    {
        public static T ReadAs<T>(this IInfluxDataApiResponse response)
        {
            return response.Body.ReadAs<T>();
        }

        public static T ReadAs<T>(this string responseBody)
        {
            return JsonConvert.DeserializeObject<T>(responseBody);
        }
    }
}