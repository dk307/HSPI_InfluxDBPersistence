using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using NullGuard;
using System;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class DeviceData
    {
        public DeviceData(IHsController HS, int refId)
        {
            this.HS = HS;
            this.refId = refId;
            var device = HS.GetDeviceByRef(refId);
            isFeatureInHS = device.Relationship == HomeSeer.PluginSdk.Devices.Identification.ERelationship.Feature;
            Data = GetDeviceImportData(device);
        }


        public string Name => HSHelper.GetName(HS, refId); 

        public ImportDeviceData Data
        {
            get
            {
                var device = HS.GetDeviceByRef(refId);
                return GetDeviceImportData(device);
            }

            set
            {
                var device = HS.GetDeviceByRef(refId);
                UpdateImportDevice(HS, device, value);
            }
        }

        public static void UpdateImportDevice(IHsController HS,
                                              int refId,
                                              in ImportDeviceData importDeviceData)
        {
            var device = HS.GetDeviceByRef(refId);
            UpdateImportDevice(HS, device, importDeviceData);
        }

        public static void UpdateImportDevice(IHsController HS,
                                              HsDevice device,
                                              ImportDeviceData importDeviceData)
        {
            string data = JsonConvert.SerializeObject(importDeviceData, Formatting.Indented);

            device.PlugExtraData.RemoveAllNamed();
            device.PlugExtraData.RemoveAllUnNamed();
            device.PlugExtraData.AddNamed(PlugInData.DevicePlugInDataNamedKey, data);

            HS.UpdatePropertyByRef(device.Ref, EProperty.PlugExtraData, device.PlugExtraData);
        }

        public virtual void Update(in double? data)
        {
            var changes = new Dictionary<EProperty, object>();

            if (data.HasValue)
            {
                changes.Add(EProperty.InvalidValue, false);
                changes.Add(EProperty.LastChange, DateTime.Now);
                changes.Add(EProperty.Value, data);
            }
            else
            {
                changes.Add(EProperty.InvalidValue, true);
            }

            if (isFeatureInHS)
            {
                HS.UpdateFeatureByRef(refId, changes);
            }
            else
            {
                HS.UpdateDeviceByRef(refId, changes);
            }
        }

        private static ImportDeviceData GetDeviceImportData(AbstractHsDevice device)
        {
            var importDeviceData = device.PlugExtraData.GetNamed<ImportDeviceData>(PlugInData.DevicePlugInDataNamedKey);
            return importDeviceData;
        }

        private readonly IHsController HS;
        private readonly bool isFeatureInHS;
        private readonly int refId;
    };
}