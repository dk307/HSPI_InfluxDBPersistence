using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using System.Collections.Generic;

namespace Hspi
{
    internal sealed class HSHelper
    {
        public HSHelper(IHsController hS)
        {
            HS = hS;
        }

        public void Fill(int deviceRefId,
                         out string measurement,
                         out string field,
                         out string fieldString,
                         out double? maxValidValue,
                         out double? minValidValue)
        {
            measurement = null;
            maxValidValue = null;
            minValidValue = null;
            field = null;
            fieldString = null;

            var device = HS.GetDeviceByRef(deviceRefId);
            var deviceTypeInfo = device.TypeInfo;

            measurement = FindTypeString(new string[] {
                                        deviceTypeInfo.SubTypeDescription,
                                        device.Name,
                                        deviceTypeInfo.Summary,});

            switch (measurement)
            {
                case "temperature":
                    maxValidValue = 255;
                    minValidValue = -255;
                    field = "value";
                    break;

                case "battery":
                case "humidity":
                    maxValidValue = 100;
                    minValidValue = 0;
                    field = "value";
                    break;

                case "watts":
                case "kwh":
                case "pressure":
                case "amperes":
                case "co2":
                case "luminance":
                case "pm25":
                    minValidValue = 0;
                    field = "value";
                    break;

                case "switch":
                case "light":
                case "window":
                case "door":
                case "lock":
                    fieldString = "fieldString";
                    break;
            }
        }

  
        private static void AddIfNotEmpty(List<string> parts, string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                parts.Add(name);
            }
        }

        private static string FindTypeString(IEnumerable<string> descriptionStrings)
        {
            foreach (var descriptionString in descriptionStrings)
            {
                var descriptionStringUpper = descriptionString.ToUpperInvariant();
                foreach (var typeString in measurementTypes)
                {
                    if (descriptionStringUpper.Contains(typeString))
                    {
#pragma warning disable CA1308 // Normalize strings to uppercase
                        return typeString.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
                    }
                }
            }

            return null;
        }

        private static string[] measurementTypes = { "TEMPERATURE", "HUMIDITY", "WATTS", "KWH", "BATTERY",
                                                     "PRESSURE", "AMPERES", "CO2", "PM25", "VOLTS", "LUMINANCE",
                                                     "LOCK", "WINDOW", "DOOR", "LIGHT", "SWITCH" };

        private readonly IHsController HS;
    }
}