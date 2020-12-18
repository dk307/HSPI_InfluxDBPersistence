using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class HS3DeviceMigrator
    {
        public HS3DeviceMigrator(IHsController HS,
                                 CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.cancellationToken = cancellationToken;
        }

        public void Migrate()
        {
            var oldPlugInConfig = new OldPlugInConfig(HS);
            var refIds = HS.GetAllRefs();

            foreach (var refId in refIds)
            {
                var device = HS.GetDeviceByRef(refId);

                if ((device != null) &&
                    (device.Interface != null) &&
                    ((device.Interface == PlugInData.PlugInName) || ((device.Interface == PlugInData.PlugInId))))
                {
                    string address = device.Address;

                    var childDeviceData = OldDeviceIdentifier.Identify(device);
                    if (childDeviceData != null)
                    {
                        if (oldPlugInConfig.ImportDevicesData.TryGetValue(childDeviceData.DeviceId,
                                                                          out var importDeviceData))
                        {
                            Trace.TraceInformation(Invariant($"Migrating {device.Name} from HS3"));

                            string data = JsonConvert.SerializeObject(importDeviceData, Formatting.Indented);

                            device.PlugExtraData.RemoveAllNamed();
                            device.PlugExtraData.RemoveAllUnNamed();
                            device.PlugExtraData.AddNamed(PlugInData.DevicePlugInDataNamedKey, data);

                            HS.UpdatePropertyByRef(device.Ref, EProperty.PlugExtraData, device.PlugExtraData);

                            // oldPlgInConfig.RemoveImportDeviceData(importDeviceData.Id);
                        }
                    }
                    else
                    {
                        if (address == OldDeviceIdentifier.CreateRootAddress())
                        {
                            Trace.TraceInformation(Invariant($"Migrating {device.Name} from HS3"));

                            string data = JsonConvert.SerializeObject(null, Formatting.Indented);

                            device.PlugExtraData.RemoveAllNamed();
                            device.PlugExtraData.RemoveAllUnNamed();
                            device.PlugExtraData.AddNamed(PlugInData.DevicePlugInDataIgnoreKey, data);

                            HS.UpdatePropertyByRef(device.Ref, EProperty.PlugExtraData, device.PlugExtraData);
                        } 
                    }

                    HS.UpdatePropertyByRef(device.Ref, EProperty.Interface, PlugInData.PlugInId);
                }

                this.cancellationToken.ThrowIfCancellationRequested();
            }
        }

        [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
        internal class OldDeviceIdentifier : System.IEquatable<OldDeviceIdentifier>
        {
            public OldDeviceIdentifier(string deviceId)
            {
                DeviceId = deviceId;
                RootDeviceAddress = CreateRootAddress();
                Address = Invariant($"{RootDeviceAddress}{AddressSeparator}{DeviceId}");
            }

            public string Address { get; }
            public string DeviceId { get; }
            public string RootDeviceAddress { get; }

            public static string CreateRootAddress() => "Root";

            public static OldDeviceIdentifier Identify(AbstractHsDevice hsDevice)
            {
                var childAddress = hsDevice.Address;

                var parts = childAddress.Split(AddressSeparator);

                if (parts.Length != 2)
                {
                    return null;
                }

                return new OldDeviceIdentifier(parts[1]);
            }

            public bool Equals(OldDeviceIdentifier other)
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

                return Equals(other as OldDeviceIdentifier);
            }

            public override int GetHashCode()
            {
                return Address.GetHashCode() ^
                       DeviceId.GetHashCode() ^
                       RootDeviceAddress.GetHashCode();
            }

            private const char AddressSeparator = '.';
        }

        private class OldPlugInConfig : PluginConfigBase
        {
            public OldPlugInConfig(IHsController HS) : base(HS)
            {
                this.importDevicesData = LoadImportDeviceSettings();
            }

            internal IDictionary<string, ImportDeviceData> ImportDevicesData => importDevicesData;

            public void RemoveImportDeviceData(string id)
            {
                importDevicesData.Remove(id);

                if (importDevicesData.Count > 0)
                {
                    SetValue(ImportDevicesIdsKey, importDevicesData.Keys.Aggregate((x, y) => x + ImportDevicesIdsSeparator + y));
                }
                else
                {
                    SetValue(ImportDevicesIdsKey, string.Empty);
                }
                ClearSection(id);
            }

            private IDictionary<string, ImportDeviceData> LoadImportDeviceSettings()
            {
                string importDevicesConcatString = GetValue(ImportDevicesIdsKey, string.Empty);
                var ids = importDevicesConcatString.Split(ImportDevicesIdsSeparator);

                var importDevicesData = new Dictionary<string, ImportDeviceData>();
                foreach (var id in ids)
                {
                    string name = GetValue(NameKey, string.Empty, id);
                    string sql = GetValue(SqlKey, string.Empty, id);
                    string time = GetValue(IntervalKey, string.Empty, id);
                    string unit = GetValue(UnitKey, string.Empty, id);

                    if (!int.TryParse(time, out int timeSeconds))
                    {
                        continue;
                    }

                    var data = new ImportDeviceData(id, name, sql, TimeSpan.FromSeconds(timeSeconds), unit);
                    importDevicesData.Add(id, data);
                }

                return importDevicesData;
            }

            private const string ImportDevicesIdsKey = "ImportDeviceIds";
            private const char ImportDevicesIdsSeparator = ',';
            private const string IntervalKey = "IntervalSeconds";
            private const string NameKey = "Name";
            private const string SqlKey = "Sql";
            private const string UnitKey = "Unit";
            private readonly IDictionary<string, ImportDeviceData> importDevicesData;
        };

        private readonly CancellationToken cancellationToken;
        private readonly IHsController HS;
    };
}