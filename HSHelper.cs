using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hspi
{
    internal sealed class HSHelper
    {
        public HSHelper(IHsController hS)
        {
            HS = hS;
            LoadSettings();
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

        public string GetName(int deviceRefId)
        {
            var device = HS.GetDeviceByRef(deviceRefId);
            return device != null ? GetName(device) : null;
        }

        public string GetName(AbstractHsDevice device)
        {
            List<string> parts = new List<string>();

            string location1 = device.Location;
            if (location2Enabled)
            {
                string location2 = device.Location2;

                if (location1First)
                {
                    AddIfNotEmpty(parts, location1);
                    AddIfNotEmpty(parts, location2);
                }
                else
                {
                    AddIfNotEmpty(parts, location2);
                    AddIfNotEmpty(parts, location1);
                }
            }
            else
            {
                AddIfNotEmpty(parts, location1);
            }

            AddIfNotEmpty(parts, device.Name);

            return string.Join(" ", parts);
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

        private void LoadSettings()
        {
            location2Enabled = Convert.ToBoolean(
                HS.GetINISetting("Settings", "bUseLocation2", Convert.ToString(false, CultureInfo.InvariantCulture), ""),
                CultureInfo.InvariantCulture);
            if (location2Enabled)
            {
                location1First = Convert.ToBoolean(
                    HS.GetINISetting("Settings", "bLocationFirst", Convert.ToString(false, CultureInfo.InvariantCulture), ""),
                    CultureInfo.InvariantCulture);
            }
            else
            {
                location1First = true;
            }
        }

        private static string[] measurementTypes = { "TEMPERATURE", "HUMIDITY", "WATTS", "KWH", "BATTERY",
                                                     "PRESSURE", "AMPERES", "CO2", "PM25", "VOLTS", "LUMINANCE",
                                                     "LOCK", "WINDOW", "DOOR", "LIGHT", "SWITCH" };

        private readonly IHsController HS;
        private bool location1First;
        private bool location2Enabled;
    }
}