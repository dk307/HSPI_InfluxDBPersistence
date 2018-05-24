using HomeSeerAPI;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    internal class NumberDeviceData : DeviceData
    {
        public NumberDeviceData(ImportDeviceData data) : base(data)
        {
        }

        public override void Update(IHSApplication HS, int refId, in double? value)
        {
            UpdateDeviceData(HS, refId, value);
        }

        public override bool StatusDevice => true;
        public override DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI DeviceAPI => DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI.Plug_In;

        public override IList<VSVGPairs.VSPair> StatusPairs
        {
            get
            {
                var pairs = new List<VSVGPairs.VSPair>();
                pairs.Add(new VSVGPairs.VSPair(HomeSeerAPI.ePairStatusControl.Status)
                {
                    PairType = VSVGPairs.VSVGPairType.Range,
                    RangeStart = int.MinValue,
                    RangeEnd = int.MaxValue,
                    IncludeValues = true,
                    RangeStatusDecimals = 3,
                    RangeStatusSuffix = string.IsNullOrEmpty(ScaleDisplayText) ? string.Empty : " @S@",
                    HasScale = !string.IsNullOrEmpty(ScaleDisplayText),
                });
                return pairs;
            }
        }

        public override IList<VSVGPairs.VGPair> GraphicsPairs => new List<VSVGPairs.VGPair>();
        public override string ScaleDisplayText => Data.Unit;
    }
}