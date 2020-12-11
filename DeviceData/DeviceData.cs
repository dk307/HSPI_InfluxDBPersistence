
using HomeSeer.PluginSdk;
using NullGuard;
using System;

namespace Hspi.DeviceData
{
    using static System.FormattableString;

    /// <summary>
    ///  Base class for Child Devices
    /// </summary>
    /// <seealso cref="Hspi.DeviceDataBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class DeviceData : DeviceDataBase
    {
        public DeviceData(ImportDeviceData data)
        {
            Data = data;
        }

        public override void SetInitialData(IHsController HS, int refId)
        {
            throw new NotImplementedException();
            //HS.SetDeviceValueByRef(refId, 0D, false);
            //HS.set_DeviceInvalidValue(refId, true);
        }

        public abstract void Update(IHsController HS, int refId, in double? deviceValue);

        
        //public override string HSDeviceTypeString => Invariant($"{PlugInData.PlugInName} Import Device");

        public ImportDeviceData Data { get; }
    };
}