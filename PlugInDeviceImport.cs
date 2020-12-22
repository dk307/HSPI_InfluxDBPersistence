using HomeSeer.PluginSdk.Devices;
using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.FormattableString;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public override bool SupportsConfigDevice => true;

        public IDictionary<string, object> GetDeviceImportData([AllowNull] string refIdString)
        {
            int refId = ParseRefId(refIdString);

            int finalRefId;
            if (HomeSeerSystem.IsRefDevice(refId))
            {
                // get the child one
                var features = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);
                if (features.Count == 1)
                {
                    finalRefId = features.First();
                }
                else
                {
                    throw new ArgumentException("Invalid Number of children");
                }
            }
            else
            {
                finalRefId = refId;
            }

            var data = new DeviceData.DeviceImportDevice(HomeSeerSystem, finalRefId);

            IDictionary<string, object> returnValue = ScribanHelper.ToDictionary(data.Data);
            returnValue["refId"] = finalRefId;
            return returnValue;
        }

        public IList<string> SaveDeviceImportData(IDictionary<string, string> deviceImportDataDict)
        {
            var errors = new List<string>();
            try
            {
                int refId = ParseRefId(deviceImportDataDict["refId"]);
                Trace.WriteLine(Invariant($"Updating device import data for Ref Id:{refId}"));

                var importDeviceData = ScribanHelper.FromDictionary<ImportDeviceData>(deviceImportDataDict);

                if (errors.Count == 0)
                {
                    // save
                    var deviceData = new DeviceData.DeviceImportDevice(HomeSeerSystem, refId);
                    deviceData.Data = importDeviceData;

                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }

        public IList<string> AddDeviceImportData(IDictionary<string, string> deviceImportDataDict)
        {
            var errors = new List<string>();
            try
            {
                string deviceName = deviceImportDataDict["name"];
                Trace.WriteLine(Invariant($"Creating new influxdb import device with name {deviceName}"));

                deviceImportDataDict["id"] = Guid.NewGuid().ToString();

                var importDeviceData = ScribanHelper.FromDictionary<ImportDeviceData>(deviceImportDataDict);

                if (errors.Count == 0)
                {
                    // add
                    DeviceData.DeviceImportDevice.CreateNew(HomeSeerSystem, deviceName, importDeviceData);
                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            return base.PostBackProc(page, data, user, userRights);
        }
    }
}