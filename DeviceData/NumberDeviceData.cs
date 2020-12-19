using HomeSeer.PluginSdk;

namespace Hspi.DeviceData
{
    internal class NumberDeviceData : DeviceData
    {
        public NumberDeviceData(IHsController HS, int refId) : base(HS, refId)
        {
        }
    }
}