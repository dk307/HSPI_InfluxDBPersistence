using HomeSeerAPI;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    internal class NumberDeviceData : DeviceData
    {
        public NumberDeviceData(DeviceType deviceType = DeviceType.DeviceInput) : base(deviceType)
        {
        }

        public override void Update(IHSApplication HS, double value)
        {
            if (!lastUpdate.HasValue || lastUpdate.Value != value)
            {
                UpdateDeviceData(HS, RefId, value);
                lastUpdate = value;
            }
        }

        public override bool StatusDevice => true;
        public override DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI DeviceAPI => DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI.Plug_In;
        public override IList<VSVGPairs.VSPair> StatusPairs => new List<VSVGPairs.VSPair>();  
        public override IList<VSVGPairs.VGPair> GraphicsPairs => new List<VSVGPairs.VGPair>();

        private double? lastUpdate = null;
    }
}