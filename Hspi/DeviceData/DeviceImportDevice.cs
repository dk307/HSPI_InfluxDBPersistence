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
    internal sealed class DeviceImportDevice
    {
        public DeviceImportDevice(IHsController HS, int refId)
        {
            this.HS = HS;
            this.refId = refId;
        }

        public static string DeviceType => "import";

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

        public int RefId => refId;

        public static DeviceImportDevice CreateNew(IHsController HS, string deviceName, ImportDeviceData data)
        {
            string logo = Path.Combine(PlugInData.PlugInId, "images", "Influxdb_logo.svg");

            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                  .WithName(deviceName)
                  .AsType(EDeviceType.Generic, 0)
                  .WithLocation(PlugInData.PlugInName)
                  .WithMiscFlags(EMiscFlag.StatusOnly)
                  .PrepareForHs();

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

            var deviceData = new DeviceImportDevice(HS, featureId);
            deviceData.Data = data;
            deviceData.Update(null);

            return deviceData;
        }

        public void Update(in double? data)
        {
            if (data.HasValue)
            {
                HS.UpdatePropertyByRef(refId, EProperty.InvalidValue, false);

                // only this call triggers events
                if (!HS.UpdateFeatureValueByRef(refId, data.Value))
                {
                    throw new Exception("Failed to update device");
                }
            }
            else
            {
                HS.UpdatePropertyByRef(refId, EProperty.InvalidValue, true);
            }
        }

        private static PlugExtraData CreatePlugInExtraData(ImportDeviceData importDeviceData)
        {
            string data = JsonConvert.SerializeObject(importDeviceData, Formatting.Indented);
            var plugExtra = new PlugExtraData();
            plugExtra.AddNamed(PlugInData.DevicePlugInDataNamedKey, data);
            plugExtra.AddNamed(PlugInData.DevicePlugInDataTypeKey, DeviceType);
            return plugExtra;
        }

        private static void UpdateImportDevice(IHsController HS,
                                               int refId,
                                               ImportDeviceData importDeviceData)
        {
            foreach (var statusGraphic in HS.GetStatusGraphicsByRef(refId))
            {
                statusGraphic.HasAdditionalData = true;

                if (statusGraphic.IsRange)
                {
                    statusGraphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
                }

                HS.AddStatusGraphicToFeature(refId, statusGraphic);
            }

            PlugExtraData plugExtra = CreatePlugInExtraData(importDeviceData);

            var changes = new Dictionary<EProperty, object>();
            changes.Add(EProperty.AdditionalStatusData, new List<string>() { importDeviceData.Unit });
            changes.Add(EProperty.PlugExtraData, plugExtra);

            HS.UpdateFeatureByRef(refId, changes);
        }

        private readonly IHsController HS;
        private readonly int refId;
    };
}