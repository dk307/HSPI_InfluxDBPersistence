using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using NullGuard;
using System;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class DeviceData
    {
        public DeviceData(bool isFeatureInHS, ImportDeviceData data)
        {
            this.isFeatureInHS = isFeatureInHS;
            Data = data;
        }

        public ImportDeviceData Data { get; }

        public virtual void Update(IHsController HS, int refId, in double? data)
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

        private readonly bool isFeatureInHS;
    };
}