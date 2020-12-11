using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using NullGuard;
using System;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    using static System.FormattableString;

    /// <summary>
    ///  Base class for Root Devices
    /// </summary>
    /// <seealso cref="Hspi.DeviceDataBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class RootDeviceData : DeviceDataBase
    {
        public override void SetInitialData(IHsController HS, int refID)
        {
            //HS.set_DeviceInvalidValue(refID, false);
            // HS.SetDeviceString(refID, "Root", false);
            throw new NotImplementedException();
        }

        public override IList<StatusControl> StatusPairs
        {
            get
            {
                var pairs = new List<StatusControl>();
                //pairs.Add(new StatusControl(HomeSeerAPI.ePairStatusControl.Status)
                //{
                //    PairType = VSVGPairs.VSVGPairType.SingleValue,
                //    Value = 0,
                //});
                return pairs;
            }
        }

        public override bool StatusDevice => false;
        public override IList<StatusGraphic> GraphicsPairs => new List<StatusGraphic>();
        //public override string HSDeviceTypeString => Invariant($"{PlugInData.PlugInName} Root Device");
    }
}