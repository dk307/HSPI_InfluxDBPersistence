using HomeSeerAPI;
using Scheduler.Classes;
using System;
using System.Collections.Generic;

namespace Hspi
{
    using static System.FormattableString;

    internal class HSHelper
    {
        public HSHelper(IHSApplication hS)
        {
            HS = hS;
            LoadSettings();
        }

        public IDictionary<int, string> GetDevices()
        {
            Dictionary<int, string> devices = new Dictionary<int, string>();
            var deviceEnumerator = HS.GetDeviceEnumerator() as clsDeviceEnumeration;
            do
            {
                DeviceClass device = deviceEnumerator.GetNext();
                if (device != null)
                {
                    devices.Add(device.get_Ref(HS), GetName(device));
                }
            }
            while (!deviceEnumerator.Finished);
            return devices;
        }

        public string GetName(int deviceRefId)
        {
            DeviceClass device = HS.GetDeviceByRef(deviceRefId) as DeviceClass;
            return device != null ? GetName(device) : null;
        }

        public string GetName(DeviceClass device)
        {
            string name;
            if (location2Enabled)
            {
                if (location1First)
                {
                    name = Invariant($"{device.get_Location(HS)} {device.get_Location2(HS)} {device.get_Name(HS)}");
                }
                else
                {
                    name = Invariant($"{device.get_Location2(HS)} {device.get_Location(HS)} {device.get_Name(HS)}");
                }
            }
            else
            {
                name = Invariant($"{device.get_Location(HS)} {device.get_Name(HS)}");
            }

            return name;
        }

        private void LoadSettings()
        {
            location2Enabled = Convert.ToBoolean(HS.GetINISetting("Settings", "bUseLocation2", Convert.ToString(false), ""));
            if (location2Enabled)
            {
                location1First = Convert.ToBoolean(HS.GetINISetting("Settings", "bLocationFirst", Convert.ToString(false), ""));
            }
            else
            {
                location1First = true;
            }
        }

        private readonly IHSApplication HS;
        private bool location2Enabled;
        private bool location1First;
    }
}