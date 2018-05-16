using HomeSeerAPI;
using NullGuard;
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
        public override void SetInitialData(IHSApplication HS, int refID)
        {
            HS.set_DeviceInvalidValue(refID, false);
            HS.SetDeviceString(refID, "Root", false);
        }

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.SingleValue,
                    Value = 0,
                });
                return pairs;
            }
        }

        public override bool StatusDevice => true;
        public override IList<VSVGPairs.VGPair> GraphicsPairs => new List<VSVGPairs.VGPair>();
        public override string HSDeviceTypeString => Invariant($"{PlugInData.PlugInName} Root Device");
        public override int HSDeviceType => (int)DeviceTypeInfo_m.DeviceTypeInfo.eDeviceType_Plugin.Root;
    }
}