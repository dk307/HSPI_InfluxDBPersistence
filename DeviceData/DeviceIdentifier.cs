using NullGuard;
using Scheduler.Classes;
using static System.FormattableString;

namespace Hspi.DeviceData
{

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class DeviceIdentifier : System.IEquatable<DeviceIdentifier>
    {
        public DeviceIdentifier(string deviceId)
        {
            DeviceId = deviceId;
            RootDeviceAddress = CreateRootAddress();
            Address = Invariant($"{RootDeviceAddress}{AddressSeparator}{DeviceId}");
        }

        public string Address { get; }
        public string DeviceId { get; }
        public string RootDeviceAddress { get; }

        public static string CreateRootAddress() => "Root";

        public static DeviceIdentifier Identify(DeviceClass hsDevice)
        {
            var childAddress = hsDevice.get_Address(null);

            var parts = childAddress.Split(AddressSeparator);

            if (parts.Length != 2)
            {
                return null;
            }

            return new DeviceIdentifier(parts[1]);
        }

        public bool Equals(DeviceIdentifier other)
        {
            if (other == null)
            {
                return false;
            }
            if (this == other)
            {
                return true;
            }
            return Address == other.Address;
        }

        public override bool Equals([AllowNull] object other)
        {
            if (other == null)
            {
                return false;
            }
            if (this == other)
            {
                return true;
            }

            return Equals(other as DeviceIdentifier);
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode() ^
                   DeviceId.GetHashCode() ^
                   RootDeviceAddress.GetHashCode();
        }

        private const char AddressSeparator = '.';
    }
}