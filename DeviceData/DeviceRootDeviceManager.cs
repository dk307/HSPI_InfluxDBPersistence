using HomeSeerAPI;
using Hspi.Exceptions;
using Hspi.Utils;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class DeviceRootDeviceManager : IDisposable
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
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var hsDevices = GetCurrentDevices();
            CreateDevices(hsDevices);
            StartDeviceFetchFromDB(hsDevices);
            Children = hsDevices.Children;
        }

        public async Task<bool> ImportDataForDevice(int refID)
        {
            if (Children.TryGetValue(refID, out var data))
            {
                await ImportDataForDevice(refID, data).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        private async Task ImportDataForDeviceInLoop(int refID, DeviceData deviceData)
        {
            while (!combinedToken.Token.IsCancellationRequested)
            {
                ImportDeviceData importDeviceData = await ImportDataForDevice(refID, deviceData).ConfigureAwait(false);
                await Task.Delay((int)Math.Min(importDeviceData.Interval.TotalMilliseconds, TimeSpan.FromDays(1).TotalMilliseconds),
                                 combinedToken.Token).ConfigureAwait(false);
            }
        }

        private async Task<ImportDeviceData> ImportDataForDevice(int refID, DeviceData deviceData)
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

            return importDeviceData;
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
                    device.set_Status_Support(HS, true);
                }
                else
                {
                    device.MISC_Set(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
                    device.MISC_Set(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
                    device.set_Status_Support(HS, false);
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
                                device.set_Status_Support(HS, true);
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

        private void StartDeviceFetchFromDB(in HSDevices hsDevices)
        {
            using (var sync = collectionTasksLock.Lock())
            {
                foreach (var childDeviceKeyValuePair in hsDevices.Children)
                {
                    int refID = childDeviceKeyValuePair.Key;
                    DeviceData deviceData = childDeviceKeyValuePair.Value;

                    this.combinedToken.Token.ThrowIfCancellationRequested();
                    collectionTasks.Add(ImportDataForDeviceInLoop(refID, deviceData));
                }
            }
        }

        private struct HSDevices
        {
            // RefId to DeviceData
            public Dictionary<int, DeviceData> Children;

            public int? ParentRefId;
        };

        private readonly IReadOnlyDictionary<int, DeviceData> Children;
        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenSource combinedToken;
        private readonly InfluxDBLoginInformation dbLoginInformation;
        private readonly IHSApplication HS;
        private readonly IReadOnlyDictionary<string, ImportDeviceData> importDevicesData;
        private readonly AsyncLock collectionTasksLock = new AsyncLock();
        private readonly List<Task> collectionTasks = new List<Task>();

        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            if (!disposedValue)
            {
                combinedToken.Cancel();
                disposedValue = true;
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        #endregion IDisposable Support
    };
}