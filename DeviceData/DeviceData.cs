using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Newtonsoft.Json;
using NullGuard;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class DeviceData
    {
        public DeviceData(IHsController HS, int refId)
        {
            this.HS = HS;
            this.refId = refId;
        }

        public ImportDeviceData Data
        {
            get
            {
                var plugInExtra = HS.GetPropertyByRef(refId, EProperty.PlugExtraData) as PlugExtraData;
                return plugInExtra?.GetNamed<ImportDeviceData>(PlugInData.DevicePlugInDataNamedKey);
            }

            set
            {
                UpdateImportDevice(HS, refId, value);
            }
        }

        public string Name => HSHelper.GetName(HS, refId);

        public static DeviceData CreateNew(IHsController HS, string deviceName, ImportDeviceData data)
        {
            string logo = Path.Combine(PlugInData.PlugInId, "images", "Influxdb_logo.svg");
            string logoCastle = Path.Combine(PlugInData.PlugInId, "images", "Influxdb_logo_castle.svg");

            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                  .WithName(deviceName)
                  .AsType(EDeviceType.Generic, 0)
                  .WithLocation(PlugInData.PlugInName)
                  .PrepareForHs();

            newDeviceData.Device.Add(EProperty.Image, logoCastle);

            int refId = HS.CreateDevice(newDeviceData);

            var plugExtra = CreatePlugInExtraData(data);

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                .WithName("Value")
                .WithLocation(PlugInData.PlugInName)
                .WithMiscFlags(EMiscFlag.ShowValues, EMiscFlag.StatusOnly)
                .AddGraphicForRange(logo, int.MinValue, int.MaxValue)
                .AsType(EFeatureType.Generic, 0)
                .WithExtraData(plugExtra)
                .PrepareForHsDevice(refId);

            int featureId = HS.CreateFeatureForDevice(newFeatureData);

            var deviceData = new DeviceData(HS, featureId);
            deviceData.Data = data;
            deviceData.Update(null);

            return deviceData;
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

            UpdateInHS(changes);
        }

        private void UpdateImportDevice(IHsController HS,
                                        int refId,
                                        ImportDeviceData importDeviceData)
        {
            foreach (var statusGraphic in HS.GetStatusGraphicsByRef(refId))
            {
                statusGraphic.HasAdditionalData = true;

                if (statusGraphic.IsRange)
                {
                    statusGraphic.TargetRange.Suffix = HsFeature.GetAdditionalDataToken(0);
                }

                HS.AddStatusGraphicToFeature(refId, statusGraphic);
            }

            PlugExtraData plugExtra = CreatePlugInExtraData(importDeviceData);

            var changes = new Dictionary<EProperty, object>();
            changes.Add(EProperty.AdditionalStatusData, new List<string>() { importDeviceData.Unit });
            changes.Add(EProperty.PlugExtraData, plugExtra);

            UpdateInHS(changes);
        }

        private static PlugExtraData CreatePlugInExtraData(ImportDeviceData importDeviceData)
        {
            string data = JsonConvert.SerializeObject(importDeviceData, Formatting.Indented);
            var plugExtra = new PlugExtraData();
            plugExtra.AddNamed(PlugInData.DevicePlugInDataNamedKey, data);
            return plugExtra;
        }

        private void UpdateInHS(Dictionary<EProperty, object> changes)
        {
            HS.UpdateFeatureByRef(refId, changes);
        }

        private readonly IHsController HS;
        private readonly int refId;
    };
}