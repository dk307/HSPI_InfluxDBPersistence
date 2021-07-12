using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using System.IO;

namespace Hspi.DeviceData
{
    internal sealed class InfluxDbStatusDevice
    {
        private InfluxDbStatusDevice(IHsController HS, int exportStatusFeatureId)
        {
            this.HS = HS;
            this.exportStatusFeatureId = exportStatusFeatureId;
        }

        private static string ConnectionStatusDeviceType => "connection-status";
        private static string RootDeviceType => "plugin-status-root";

        public static InfluxDbStatusDevice CreateOrGet(IHsController HS)
        {
            _ = HS.GetRefsByInterface(PlugInData.PlugInId);
            var (rootDeviceId, exportStatusFeatureId) = FindDevice(HS);

            if (!rootDeviceId.HasValue)
            {
                rootDeviceId = CreateRootDevice(HS);
                exportStatusFeatureId = null;
            }
            if (!exportStatusFeatureId.HasValue)
            {
                exportStatusFeatureId = CreateExportStatusFeature(HS, rootDeviceId.Value);
            }

            var device = new InfluxDbStatusDevice(HS,  exportStatusFeatureId.Value);
            return device;
        }

        public void UpdateExportConnectionStatus(bool working)
        {
            HSDeviceHelper.UpdateDeviceValue(HS, exportStatusFeatureId, working ? OnValue : OffValue);
        }

        private static int CreateExportStatusFeature(IHsController HS, int parentDeviceId)
        {
            string onImagePath = Path.Combine("images", "HomeSeer", "status", "on.gif");
            string offImagePath = Path.Combine("images", "HomeSeer", "status", "off.gif");

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                .WithName("History Export Status")
                .WithLocation(PlugInData.PlugInName)
                .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                .AsType(EFeatureType.Generic, 0)
                .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(ConnectionStatusDeviceType))
                .WithDefaultValue(0)
                .AddButton(OffValue, "Not-working", controlUse: HomeSeer.PluginSdk.Devices.Controls.EControlUse.Off)
                .AddButton(OnValue, "Working", controlUse: HomeSeer.PluginSdk.Devices.Controls.EControlUse.On)
                .AddGraphicForValue(offImagePath, OffValue)
                .AddGraphicForValue(onImagePath, OnValue)
                .PrepareForHsDevice(parentDeviceId);

            return HS.CreateFeatureForDevice(newFeatureData);
        }

        private static int CreateRootDevice(IHsController HS)
        {
            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                                    .WithName("Influx DB History PlugIn Root")
                                    .AsType(EDeviceType.Generic, 0)
                                    .WithLocation(PlugInData.PlugInName)
                                    .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                                    .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(RootDeviceType))
                                    .PrepareForHs();

            int refId = HS.CreateDevice(newDeviceData);
            return refId;
        }

        private static (int?, int?) FindDevice(IHsController HS)
        {
            int? rootDeviceId = null;
            int? connectionStatusFeatureId = null;
            var refIds = HS.GetRefsByInterface(PlugInData.PlugInId);

            foreach (var refId in refIds)
            {
                ERelationship relationship = (ERelationship)HS.GetPropertyByRef(refId, EProperty.Relationship);
                if (relationship == ERelationship.Device)
                {
                    var deviceType = HSDeviceHelper.GetDeviceTypeFromPlugInData(HS, refId);
                    if (deviceType == RootDeviceType)
                    {
                        rootDeviceId = refId;
                    }
                }
                else if (relationship == ERelationship.Feature)
                {
                    var deviceType = HSDeviceHelper.GetDeviceTypeFromPlugInData(HS, refId);
                    if (deviceType == ConnectionStatusDeviceType)
                    {
                        connectionStatusFeatureId = refId;
                    }
                }
            }

            return (rootDeviceId, connectionStatusFeatureId);
        }

        private const int OffValue = 0;
        private const int OnValue = 1;
        private readonly int exportStatusFeatureId;
        private readonly IHsController HS;
    };
}