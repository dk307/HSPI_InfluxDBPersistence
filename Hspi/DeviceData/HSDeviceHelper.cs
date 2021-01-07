using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using NullGuard;
using System;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal static class HSDeviceHelper
    {
        public static PlugExtraData CreatePlugInExtraDataFroDeviceType(string deviceType)
        {
            var plugExtra = new PlugExtraData();
            plugExtra.AddNamed(PlugInData.DevicePlugInDataTypeKey, deviceType);
            return plugExtra;
        }

        public static string GetDeviceTypeFromPlugInData(IHsController HS, int refId)
        {
            var plugInExtra = HS.GetPropertyByRef(refId, EProperty.PlugExtraData) as PlugExtraData;
            if (plugInExtra != null)
            {
                if (plugInExtra.NamedKeys.Contains(PlugInData.DevicePlugInDataTypeKey))
                {
                    return plugInExtra[PlugInData.DevicePlugInDataTypeKey];
                }
            }

            return null;
        }

        public static void UpdateDeviceValue(IHsController HS, int refId, in double? data)
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
    }
}