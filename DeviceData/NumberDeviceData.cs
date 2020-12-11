using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    internal class NumberDeviceData : DeviceData
    {
        public NumberDeviceData(ImportDeviceData data) : base(data)
        {
        }

        public override void Update(IHsController HS, int refId, in double? value)
        {
            UpdateDeviceData(HS, refId, value);
        }

        public override bool StatusDevice => true;
  
        public override IList<StatusControl> StatusPairs
        {
            get
            {
                var pairs = new List<StatusControl>();
                //pairs.Add(new StatusControl(ePairStatusControl.Status)
                //{
                //    PairType = StatusControl.VSVGPairType.Range,
                //    RangeStart = int.MinValue,
                //    RangeEnd = int.MaxValue,
                //    IncludeValues = true,
                //    RangeStatusDecimals = 3,
                //    RangeStatusSuffix = string.IsNullOrEmpty(ScaleDisplayText) ? string.Empty : " @S@",
                //    HasScale = !string.IsNullOrEmpty(ScaleDisplayText),
                //});
                return pairs;
            }
        }

        public override IList<StatusGraphic> GraphicsPairs => new List<StatusGraphic>();
        public override string ScaleDisplayText => Data.Unit;
    }
}