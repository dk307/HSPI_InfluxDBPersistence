using HomeSeerAPI;
using Hspi.DeviceData;
using Hspi.Utils;
using NullGuard;
using Scheduler;
using Scheduler.Classes;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;

namespace Hspi
{
    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        public IPlugInAPI.strMultiReturn GetRefreshActionPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        {
            IPlugInAPI.strMultiReturn result = default;
            result.DataOut = actionInfo.DataIn;
            result.TrigActInfo = actionInfo;
            result.sResult = string.Empty;
            if (postData != null && postData.Count > 0)
            {
                RefreshDeviceAction action = (actionInfo.DataIn != null) ?
                                                    (RefreshDeviceAction)ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) :
                                                    new RefreshDeviceAction();

                foreach (var pair in postData)
                {
                    string text = Convert.ToString(pair);
                    if (!string.IsNullOrWhiteSpace(text) && text.StartsWith(RefreshActionUIDropDownName))
                    {
                        action.DeviceRefId = Convert.ToInt32(postData[text]);
                    }
                }

                result.DataOut = ObjectSerialize.SerializeToBytes(action);
            }

            return result;
        }

        public string GetRefreshActionUI(string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        {
            StringBuilder stb = new StringBuilder();
            var currentDevices = GetCurrentDeviceImportDevices();
            RefreshDeviceAction refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;

            string selection = string.Empty;
            if (refreshDeviceAction != null)
            {
                selection = refreshDeviceAction.DeviceRefId.ToString(CultureInfo.InvariantCulture);
            }

            stb.Append(FormDropDown(RefreshActionUIDropDownName + uniqueControlId, currentDevices, selection, 400, string.Empty, true, "Events"));
            return stb.ToString();
        }

        private NameValueCollection GetCurrentDeviceImportDevices()
        {
            HSHelper hsHelper = new HSHelper(HS);
            var deviceEnumerator = HS.GetDeviceEnumerator() as clsDeviceEnumeration;

            var currentDevices = new NameValueCollection();
            var importDevicesData = pluginConfig.ImportDevicesData;
            do
            {
                DeviceClass device = deviceEnumerator.GetNext();
                if ((device != null) &&
                    (device.get_Interface(HS) != null) &&
                    (device.get_Interface(HS).Trim() == PlugInData.PlugInName))
                {
                    string address = device.get_Address(HS);

                    var childDeviceData = DeviceIdentifier.Identify(device);
                    if (childDeviceData != null)
                    {
                        if (pluginConfig.ImportDevicesData.TryGetValue(childDeviceData.DeviceId, out var importDeviceData))
                        {
                            currentDevices.Add(device.get_Ref(HS).ToString(CultureInfo.CurrentCulture), hsHelper.GetName(device));
                        }
                    }
                }
            } while (!deviceEnumerator.Finished);

            return currentDevices;
        }

        private const string RefreshActionUIDropDownName = "RefreshActionUIDropDownName_";
    }
}