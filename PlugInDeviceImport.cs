using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.FormattableString;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public override bool SupportsConfigDevice => true;

        public IDictionary<string, object> GetDeviceImportData([AllowNull] string refIdString)
        {
            int refId = ParseRefId(refIdString);
            var data = new DeviceData.DeviceData(HomeSeerSystem, refId);

            return ScribanHelper.ToDictionary(data.Data);
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
                    var deviceData = new DeviceData.DeviceData(HomeSeerSystem, refId);
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
                    DeviceData.DeviceData.CreateNew(HomeSeerSystem, deviceName, importDeviceData);
                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }
    }
}