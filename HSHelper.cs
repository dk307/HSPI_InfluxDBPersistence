
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
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

        public IDictionary<int, string> GetDevices()
        {
            Dictionary<int, string> devices = new Dictionary<int, string>();
            var deviceEnumerator = HS.GetAllDevices(true);
            foreach(var device in deviceEnumerator)
            {
                if (device != null)
                {
                    devices.Add(device.Ref, GetName(device));
                }
            }
           
            return devices;
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

        public void Fill(int deviceRefId, out string measurement, out double? maxValidValue, out double? minValidValue)
        {
            measurement = null;
            maxValidValue = null;
            minValidValue = null;

            throw new NotImplementedException();

            //var device = HS.GetDeviceByRef(deviceRefId);
            //var deviceType = device.get_DeviceType_Get(HS);
            //measurement = FindTypeString(new string[] {
            //                          deviceType?.Device_SubType_Description,
            //                          deviceType?.Device_API_Description,
            //                          device.get_Name(HS) });

            //switch (measurement)
            //{
            //    case "temperature":
            //        maxValidValue = 255;
            //        minValidValue = -255;
            //        break;

            //    case "battery":
            //    case "humidity":
            //        maxValidValue = 100;
            //        minValidValue = 0;
            //        break;

            //    case "watts":
            //    case "kwh":
            //    case "pressure":
            //    case "amperes":
            //    case "co2":
            //    case "luminance":
            //    case "pm25":
            //        minValidValue = 0;
            //        break;
            //}
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

        private readonly IHsController HS;
        private bool location2Enabled;
        private bool location1First;

        private static string[] measurementTypes = { "TEMPERATURE", "HUMIDITY", "WATTS", "KWH", "BATTERY",
                                                     "PRESSURE", "AMPERES", "CO2", "PM25", "VOLTS", "LUMINANCE" };
    }
}