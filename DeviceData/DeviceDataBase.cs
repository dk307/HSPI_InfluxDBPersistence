using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using NullGuard;
using System;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    /// <summary>
    /// This is base class for creating and updating devices in HomeSeer.
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class DeviceDataBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceDataBase" /> class.
        /// </summary>
        /// <param name="name">Name of the Device</param>
        protected DeviceDataBase()
        {
        }

        /// <summary>
        /// Gets the status pairs for creating device.
        /// </summary>
        /// <param name="config">The plugin configuration.</param>
        /// <returns></returns>
        public abstract IList<StatusControl> StatusPairs { get; }

        /// <summary>
        /// Gets the graphics pairs for creating device
        /// </summary>
        /// <param name="config">The plugin configuration.</param>
        /// <returns></returns>
        public abstract IList<StatusGraphic> GraphicsPairs { get; }

        
        public virtual EDeviceType DeviceType => EDeviceType.Generic;
        
        public abstract bool StatusDevice { get; }

        public virtual string ScaleDisplayText => string.Empty;

        /// <summary>
        /// Sets the initial data for the device.
        /// </summary>
        /// <param name="HS">The hs.</param>
        /// <param name="RefId">The reference identifier.</param>
        public abstract void SetInitialData(IHsController HS, int RefId);

        /// <summary>
        /// Updates the device data from number data
        /// </summary>
        /// <param name="HS">Homeseer application.</param>
        /// <param name="refId">The reference identifier.</param>
        /// <param name="data">Number data.</param>
        protected static void UpdateDeviceData(IHsController HS, int refId, in double? data)
        {

            //if (data.HasValue)
            //{
            //    HS.set_DeviceInvalidValue(refId, false);
            //    HS.SetDeviceValueByRef(refId, data.Value, true);
            //    HS.SetDeviceLastChange(refId, DateTime.Now);
            //}
            //else
            //{
            //    HS.set_DeviceInvalidValue(refId, true);
            //}

            throw new NotImplementedException();
        }
    };
}