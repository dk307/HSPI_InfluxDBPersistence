using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Hspi.Utils
{
    internal static class ScribanHelper
    {
        public static T FromDictionary<T>(IDictionary<string, string> source) where T : class
        {
            var constructors = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();

                var constructorParameters = new List<object>();
                foreach (var parameter in parameters)
                {
                    string normalizedName = NormalizeName(parameter.Name);
                    if (source.TryGetValue(normalizedName, out var sourceValue))
                    {
                        try
                        {
                            object convertedValue = ConvertToParameterExpectedType(parameter, sourceValue);
                            constructorParameters.Add(convertedValue);
                        }
                        catch
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (parameter.IsOptional)
                        {
                            constructorParameters.Add(parameter.DefaultValue);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return (T)constructor.Invoke(constructorParameters.ToArray());
            }

            throw new ArgumentException("None of constructors match");
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

        private static object ConvertToParameterExpectedType(ParameterInfo parameter, string sourceValue)
        {
            Type sourceType = typeof(string);
            Type expectedType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

            if (expectedType.IsAssignableFrom(sourceType))
            {
                return sourceValue;
            }

            // Nullable value
            if (string.IsNullOrEmpty(sourceValue) && 
                (!parameter.ParameterType.IsValueType || (Nullable.GetUnderlyingType(parameter.ParameterType) != null)))
            {
                return null;
            }

            if (expectedType.IsEnum)
            {
                return Enum.Parse(expectedType, sourceValue);
            }

            return Convert.ChangeType(sourceValue, expectedType, CultureInfo.InvariantCulture);
        }

        private static string NormalizeName(string name)
        {
            return name.ToLower(CultureInfo.InvariantCulture);
        }
    }
}