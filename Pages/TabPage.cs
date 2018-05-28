using HomeSeerAPI;
using Hspi.DeviceData;
using NullGuard;
using Scheduler;
using Scheduler.Classes;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hspi
{
    using static System.FormattableString;

    [Serializable]
    internal class RefreshDeviceAction
    {
        public int DeviceRefId;
    }

    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private const string RefreshActionUIDropDownName = "Device_";

        public string GetDeviceHistoryTab(DeviceClass deviceClass)
        {
            int refId = deviceClass.get_Ref(HS);
            var dataKeyPair = pluginConfig.DevicePersistenceData.SingleOrDefault(x => x.Value.DeviceRefId == refId);
            var data = dataKeyPair.Value;
            if (data != null)
            {
                var fields = GetFields(data);
                string lastValuesQuery = Invariant($"SELECT {string.Join(",", fields)} from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' ORDER BY time DESC LIMIT 100");

                StringBuilder stb = new StringBuilder();
                IncludeDataTableFiles(stb);

                stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                stb.Append("<tr height='5'><td style='width:25%'></td><td style='width:20%'></td><td style='width:55%'></td></tr>");
                stb.Append(Invariant($"<tr><td class='tableheader' colspan=3>History</td></tr>"));
                stb.Append("<tr><td class='tablecell' colspan=3>");
                BuildTable(lastValuesQuery, stb, 10);
                stb.Append("</td></tr>");
                stb.Append(Invariant($"</td><td></td></tr>"));
                stb.Append("<tr height='5'><td colspan=3></td></tr>");
                stb.Append("<tr><td colspan=3>");
                stb.Append(PageTypeButton(Invariant($"Edit{data.Id}"), "Edit", EditDevicePageType, id: data.Id));
                stb.Append("&nbsp;");
                stb.Append(PageTypeButton(Invariant($"Queries{data.Id}"), "More Queries", HistoryDevicePageType, id: data.Id));
                stb.Append("</td></tr>");
                stb.Append(@"</table>");

                return stb.ToString();
            }

            return string.Empty;
        }

        public string GetDeviceImportTab(DeviceIdentifier deviceIdentifier)
        {
            foreach (var device in pluginConfig.ImportDevicesData)
            {
                if (device.Key == deviceIdentifier.DeviceId)
                {
                    StringBuilder stb = new StringBuilder();

                    stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                    stb.Append("<tr height='5'><td style='width:25%'></td><td style='width:20%'></td><td style='width:55%'></td></tr>");
                    stb.Append(Invariant($"<tr><td class='tableheader' colspan=3>Import Settings</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell' colspan=2>{device.Value.Name ?? string.Empty}</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Sql:</td><td class='tablecell' colspan=2>{device.Value.Sql ?? string.Empty}</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Refresh Interval(seconds):</td><td class='tablecell' colspan=2>{device.Value.Interval.TotalSeconds}</td></tr>"));
                    stb.Append(Invariant($"<tr><td class='tablecell'>Unit:</td><td class='tablecell' colspan=2>{device.Value.Unit ?? string.Empty}</td></tr>"));
                    stb.Append(Invariant($"</td><td></td></tr>"));
                    stb.Append("<tr height='5'><td colspan=3></td></tr>");
                    stb.Append("<tr><td colspan=3>");
                    stb.Append(PageTypeButton(Invariant($"Edit{device.Value.Id}"), "Edit", EditDeviceImportPageType, id: device.Value.Id));
                    stb.Append("</td></tr>");

                    stb.Append(@" </table>");

                    return stb.ToString();
                }
            }
            return string.Empty;
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
    }
}