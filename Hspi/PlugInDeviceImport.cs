using HomeSeer.PluginSdk.Devices;
using Hspi.DeviceData;
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
                logger.Debug(Invariant($"Updating device import data for Ref Id:{refId}"));

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

        public IDictionary<string, object> AddDeviceImportData(IDictionary<string, string> deviceImportDataDict)
        {
            DeviceImportDevice device = null;
            var errors = new List<string>();
            try
            {
                string deviceName = deviceImportDataDict["name"];
                logger.Debug(Invariant($"Creating new influxdb import device with name {deviceName}"));

                deviceImportDataDict["id"] = Guid.NewGuid().ToString();

                var importDeviceData = ScribanHelper.FromDictionary<ImportDeviceData>(deviceImportDataDict);

                if (errors.Count == 0)
                {
                    // add
                    device = DeviceImportDevice.CreateNew(HomeSeerSystem, deviceName, importDeviceData);
                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
             
            var data = new Dictionary<string, object>();
            data.Add("refId", device?.RefId);
            data.Add("error", errors);

            return data;
        }
    }
}