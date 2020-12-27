using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Hspi.Utils
{
    internal static class ScribanHelper
    {
        public static IDictionary<string, object> ConvertToStringObjectDictionary(IDictionary<string, string> source)
        {
            var destination = new Dictionary<string, object>();
            foreach (var pair in source)
            {
                destination.Add(pair.Key, pair.Value);
            }
            return destination;
        }

        public static T FromDictionary<T>(IDictionary<string, string> source) where T : class
        {
            var json = JsonConvert.SerializeObject(source);
            var obj = JsonConvert.DeserializeObject<T>(json);
            return obj;
        }

        public static T FromDictionary<T>(IDictionary<string, object> source) where T : class
        {
            var json = JsonConvert.SerializeObject(source);
            var obj = JsonConvert.DeserializeObject<T>(json);
            return obj;
        }

        public static IDictionary<string, object> ToDictionary<T>(T obj)
        {
            var dict = new Dictionary<string, object>();

            foreach (var propertyInfo in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var currentValue = propertyInfo.GetValue(obj);
                dict.Add(NormalizeName(propertyInfo.Name), currentValue);
            }

            return dict;
        }

        private static string NormalizeName(string name)
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            return name.ToLower(CultureInfo.InvariantCulture);
#pragma warning restore CA1308 // Normalize strings to uppercase
        }
    }
}