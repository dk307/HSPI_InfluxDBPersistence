using HomeSeerAPI;
using Hspi.Exceptions;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Hspi.DeviceData
{
    using static System.FormattableString;

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class DeviceRootDeviceManager
    {
        public DeviceRootDeviceManager(IHSApplication HS,
                                       InfluxDBLoginInformation dbLoginInformation,
                                       IReadOnlyDictionary<string, ImportDeviceData> importDevicesData,
                                       CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.dbLoginInformation = dbLoginInformation;
            this.importDevicesData = importDevicesData;
            this.cancellationToken = cancellationToken;
            combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                                                                                      cancellationTokenSourceForUpdateDevice.Token);
            var deviceData = GetCurrentDevices();
            CreateDevices(deviceData);
        }

        /// <summary>
        /// Creates the HS device.
        /// </summary>
        /// <param name="optionalParentRefId">The optional parent reference identifier.</param>
        /// <param name="name">The name of device</param>
        /// <param name="deviceAddress">The device address.</param>
        /// <param name="deviceData">The device data.</param>
        /// <returns>
        /// New Device
        /// </returns>
        private DeviceClass CreateDevice(int? optionalParentRefId, string name, string deviceAddress, DeviceDataBase deviceData)
        {
            Trace.TraceInformation(Invariant($"Creating Device with Address:{deviceAddress}"));

            DeviceClass device = null;
            int refId = HS.NewDeviceRef(name);
            if (refId > 0)
            {
                device = (DeviceClass)HS.GetDeviceByRef(refId);
                string address = deviceAddress;
                device.set_Address(HS, address);
                device.set_Device_Type_String(HS, deviceData.HSDeviceTypeString);
                var deviceType = new DeviceTypeInfo_m.DeviceTypeInfo();
                deviceType.Device_API = deviceData.DeviceAPI;
                deviceType.Device_Type = deviceData.HSDeviceType;

                device.set_DeviceType_Set(HS, deviceType);
                device.set_Interface(HS, PlugInData.PlugInName);
                device.set_InterfaceInstance(HS, string.Empty);
                device.set_Last_Change(HS, DateTime.Now);
                device.set_Location(HS, PlugInData.PlugInName);

                device.MISC_Set(HS, Enums.dvMISC.SHOW_VALUES);
                if (deviceData.StatusDevice)
                {
                    device.MISC_Set(HS, Enums.dvMISC.STATUS_ONLY);
                    device.MISC_Clear(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
                    device.MISC_Clear(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
                    device.set_Status_Support(HS, false);
                }
                else
                {
                    device.MISC_Set(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
                    device.MISC_Set(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
                    device.set_Status_Support(HS, true);
                }

                var pairs = deviceData.StatusPairs;
                foreach (var pair in pairs)
                {
                    HS.DeviceVSP_AddPair(refId, pair);
                }

                var gPairs = deviceData.GraphicsPairs;
                foreach (var gpair in gPairs)
                {
                    HS.DeviceVGP_AddPair(refId, gpair);
                }

                DeviceClass parent = null;
                if (optionalParentRefId.HasValue)
                {
                    parent = (DeviceClass)HS.GetDeviceByRef(optionalParentRefId.Value);
                }

                if (parent != null)
                {
                    parent.set_Relationship(HS, Enums.eRelationship.Parent_Root);
                    device.set_Relationship(HS, Enums.eRelationship.Child);
                    device.AssociatedDevice_Add(HS, parent.get_Ref(HS));
                    parent.AssociatedDevice_Add(HS, device.get_Ref(HS));
                }

                deviceData.SetInitialData(HS, refId);
            }

            return device;
        }

        /// <summary>
        /// Creates the devices based on configuration.
        /// </summary>
        /// <param name="currentDevices">The current HS devices.</param>
        /// <param name="token">The token.</param>
        private void CreateDevices(HSDevices hsDevices)
        {
            try
            {
                string parentAddress = DeviceIdentifier.CreateRootAddress();
                foreach (var deviceImport in importDevicesData)
                {
                    combinedToken.Token.ThrowIfCancellationRequested();
                    var deviceIdentifier = new DeviceIdentifier(deviceImport.Key);

                    hsDevices.Children.TryGetValue(deviceIdentifier, out DeviceClass device);

                    if (device == null)
                    {
                        // lazy creation of parent device when child is created
                        if (hsDevices.Parent == null)
                        {
                            hsDevices.Parent = CreateDevice(null, "Root", deviceIdentifier.RootDeviceAddress, new RootDeviceData());
                        }

                        string address = deviceIdentifier.Address;
                        var childDevice = new NumberDeviceData();

                        var childHSDevice = CreateDevice(hsDevices.Parent.get_Ref(HS), deviceImport.Value.Name, address, childDevice);
                        hsDevices.Children[deviceIdentifier] = childHSDevice;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to Create Devices For PlugIn With {ex.GetFullMessage()}"));
            }
        }

        private HSDevices GetCurrentDevices()
        {
            var deviceEnumerator = HS.GetDeviceEnumerator() as clsDeviceEnumeration;

            if (deviceEnumerator == null)
            {
                throw new HspiException(Invariant($"{PlugInData.PlugInName} failed to get a device enumerator from HomeSeer."));
            }

            DeviceClass parentDeviceClass = null;
            var currentChildDevices = new Dictionary<DeviceIdentifier, DeviceClass>();

            string parentAddress = DeviceIdentifier.CreateRootAddress();
            do
            {
                DeviceClass device = deviceEnumerator.GetNext();
                if ((device != null) &&
                    (device.get_Interface(HS) != null) &&
                    (device.get_Interface(HS).Trim() == PlugInData.PlugInName))
                {
                    string address = device.get_Address(HS);
                    if (address == parentAddress)
                    {
                        parentDeviceClass = device;
                    }
                    else
                    {
                        var childDeviceData = DeviceIdentifier.Identify(device);
                        if (childDeviceData != null)
                        {
                            currentChildDevices.Add(childDeviceData, device);
                        }
                    }
                }
            } while (!deviceEnumerator.Finished);

            return new HSDevices()
            {
                Parent = parentDeviceClass,
                Children = currentChildDevices,
            };
        }

        private void StartDeviceFetchFromDB(in HSDevices hSDevices)
        {
            foreach (var childDevice in hSDevices.Children)
            {
                if (importDevicesData.TryGetValue(childDevice.Key.DeviceId, out var importDeviceData))
                {
                    //start as task to fetch data
                }
            }
        }

        private struct HSDevices
        {
            public IDictionary<DeviceIdentifier, DeviceClass> Children;
            public DeviceClass Parent;
        };

        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenSource cancellationTokenSourceForUpdateDevice = new CancellationTokenSource();
        private readonly CancellationTokenSource combinedToken;
        private readonly InfluxDBLoginInformation dbLoginInformation;
        private readonly IHSApplication HS;
        private readonly IReadOnlyDictionary<string, ImportDeviceData> importDevicesData;
    };
}