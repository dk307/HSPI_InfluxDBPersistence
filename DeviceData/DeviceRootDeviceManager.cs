using HomeSeerAPI;
using Hspi.Exceptions;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.DeviceData
{
    using static System.FormattableString;

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class DeviceRootDeviceManager : IDisposable
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
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                                                                            cancellationTokenSourceForUpdateDevice.Token);
            var hsDevices = GetCurrentDevices();
            CreateDevices(hsDevices);
            StartDeviceFetchFromDB(hsDevices);
        }

        public void Cancel()
        {
            cancellationTokenSourceForUpdateDevice.Cancel();
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

                if (!string.IsNullOrEmpty(deviceData.ScaleDisplayText))
                {
                    device.set_ScaleText(HS, deviceData.ScaleDisplayText);
                }

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
                var existingDevices = hsDevices.Children.ToDictionary(x => x.Value.Data.Id);
                foreach (var deviceImport in importDevicesData)
                {
                    combinedToken.Token.ThrowIfCancellationRequested();

                    if (!existingDevices.TryGetValue(deviceImport.Key, out var device))
                    {
                        DeviceIdentifier deviceIdentifier = new DeviceIdentifier(deviceImport.Value.Id);
                        // lazy creation of parent device when child is created
                        if (!hsDevices.ParentRefId.HasValue)
                        {
                            var parentDeviceClass = CreateDevice(null, Invariant($"{PlugInData.PlugInName} Root"),
                                                                 deviceIdentifier.RootDeviceAddress, new RootDeviceData());
                            hsDevices.ParentRefId = parentDeviceClass.get_Ref(HS);
                        }

                        string address = deviceIdentifier.Address;
                        var childDevice = new NumberDeviceData(deviceImport.Value);

                        var childHSDevice = CreateDevice(hsDevices.ParentRefId.Value, deviceImport.Value.Name, address, childDevice);
                        hsDevices.Children[childHSDevice.get_Ref(HS)] = childDevice;
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

            int? parentRefId = null;
            var currentChildDevices = new Dictionary<int, DeviceData>();

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
                        parentRefId = device.get_Ref(HS);
                    }
                    else
                    {
                        var childDeviceData = DeviceIdentifier.Identify(device);
                        if (childDeviceData != null)
                        {
                            if (importDevicesData.TryGetValue(childDeviceData.DeviceId, out var importDeviceData))
                            {
                                currentChildDevices.Add(device.get_Ref(HS), new NumberDeviceData(importDeviceData));
                            }
                        }
                    }
                }
            } while (!deviceEnumerator.Finished);

            return new HSDevices()
            {
                ParentRefId = parentRefId,
                Children = currentChildDevices,
            };
        }

        private async Task ImportDataForDevice(int refID, DeviceData deviceData)
        {
            while (!combinedToken.Token.IsCancellationRequested)
            {
                ImportDeviceData importDeviceData = deviceData.Data;

                //start as task to fetch data
                double? deviceValue = null;
                try
                {
                    var queryData = await InfluxDBHelper.GetSingleValueForQuery(importDeviceData.Sql, dbLoginInformation).ConfigureAwait(false);
                    deviceValue = Convert.ToDouble(queryData);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"Failed to get value from Db for {importDeviceData.Name} with {ex.GetFullMessage()}"));
                }

                try
                {
                    deviceData.Update(HS, refID, deviceValue);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"Failed to write value to HS for {importDeviceData.Name} with {ex.GetFullMessage()}"));
                }
                await Task.Delay((int)Math.Min(importDeviceData.Interval.TotalMilliseconds, TimeSpan.FromDays(1).TotalMilliseconds), combinedToken.Token).ConfigureAwait(false);
            }
        }

        private void StartDeviceFetchFromDB(HSDevices hSDevices)
        {
            foreach (var childDeviceKeyValuePair in hSDevices.Children)
            {
                int refID = childDeviceKeyValuePair.Key;
                DeviceData deviceData = childDeviceKeyValuePair.Value;

                Task.Factory.StartNew(() => ImportDataForDevice(refID, deviceData),
                                      cancellationTokenSourceForUpdateDevice.Token,
                                      TaskCreationOptions.RunContinuationsAsynchronously,
                                      TaskScheduler.Current);
            }
        }

        private struct HSDevices
        {
            // RefId to DeviceData
            public IDictionary<int, DeviceData> Children;

            public int? ParentRefId;
        };

        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenSource cancellationTokenSourceForUpdateDevice = new CancellationTokenSource();
        private readonly CancellationTokenSource combinedToken;
        private readonly InfluxDBLoginInformation dbLoginInformation;
        private readonly IHSApplication HS;
        private readonly IReadOnlyDictionary<string, ImportDeviceData> importDevicesData;

        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        private bool disposedValue = false; // To detect redundant calls
        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DeviceRootDeviceManager() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    };
}