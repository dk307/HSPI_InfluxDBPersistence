using HomeSeer.PluginSdk;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class DeviceRootDeviceManager : IDisposable
    {
        public DeviceRootDeviceManager(IHsController HS,
                                       InfluxDBLoginInformation dbLoginInformation,

                                       CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.dbLoginInformation = dbLoginInformation;
            this.cancellationToken = cancellationToken;
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            MigrateDevices();

            var hsDevices = GetCurrentDevices();

            ImportDevices = hsDevices;

            StartDeviceFetchFromDB();
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            if (!disposedValue)
            {
                combinedToken.Cancel();
                disposedValue = true;
            }
        }

        public async Task<bool> ImportDataForDevice(int refID)
        {
            if (ImportDevices.TryGetValue(refID, out var data))
            {
                await ImportDataForDevice(refID, data).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        private Dictionary<int, DeviceData> GetCurrentDevices()
        {
            var refIds = HS.GetAllRefs();

            var currentChildDevices = new Dictionary<int, DeviceData>();

            foreach (var refId in refIds)
            {
                var device = HS.GetDeviceByRef(refId);

                if ((device != null) &&
                    (device.Interface != null) &&
                    (device.Interface == PlugInData.PlugInName))
                {
                    string name = HS.GetNameByRef(refId);
                    if (device.PlugExtraData.ContainsNamed(PlugInData.DevicePlugInDataNamedKey))
                    {
                        try
                        {
                            var importDeviceData = device.PlugExtraData.GetNamed<ImportDeviceData>(PlugInData.DevicePlugInDataNamedKey);

                            bool isFeature = device.Relationship == HomeSeer.PluginSdk.Devices.Identification.ERelationship.Feature;
                            currentChildDevices.Add(device.Ref, new NumberDeviceData(isFeature, importDeviceData));
                            Trace.TraceInformation(Invariant($"{device.Name} found"));
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceWarning(Invariant($"{device.Name} has invalid plugin data load failed with {ex.GetFullMessage()}. Please recreate it."));
                        }
                    }
                    else
                    {
                        if (!device.PlugExtraData.ContainsNamed(PlugInData.DevicePlugInDataIgnoreKey))
                        {
                            Trace.TraceWarning(Invariant($"{device.Name} has invalid plugin data. Please recreate it."));
                        }
                    }
                }
            }

            return currentChildDevices;
        }

        private async Task<ImportDeviceData> ImportDataForDevice(int refID, DeviceData deviceData)
        {
            ImportDeviceData importDeviceData = deviceData.Data;

            //start as task to fetch data
            double? deviceValue = null;
            try
            {
                var queryData = await InfluxDBHelper.GetSingleValueForQuery(importDeviceData.Sql, dbLoginInformation).ConfigureAwait(false);
                deviceValue = Convert.ToDouble(queryData, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to get value from Db for {importDeviceData.Name} with {ex.GetFullMessage()}"));
            }

            try
            {
                Trace.WriteLine(Invariant($"Updating {importDeviceData.Name} with {deviceValue}"));
                deviceData.Update(HS, refID, deviceValue);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to write value to HS for {importDeviceData.Name} with {ex.GetFullMessage()}"));
            }

            return importDeviceData;
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
        //private AbstractHsDevice CreateDevice(int? optionalParentRefId, string name, string deviceAddress, HSDeviceCore deviceData)
        //{
        //    Trace.TraceInformation(Invariant($"Creating Device with Address:{deviceAddress}"));

        //    //var plugExtraData = new PlugExtraData();
        //plugExtraData.AddNamed(DeviceIdentifier.ExtraDataNamedData, deviceAddress);

        //var deviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
        //                              .WithExtraData(plugExtraData)
        //                              .AsType(deviceData.DeviceAPI,  )
        //                              .WithLocation(PlugInData.PlugInName)
        //                              .WithName(name)
        //                              .PrepareForHs();

        //int refId = HS.CreateDevice(deviceData);
        //if (refId > 0)
        //{
        //    device.set_Device_Type_String(HS, deviceData.HSDeviceTypeString);
        //    var deviceType = new DeviceTypeInfo_m.DeviceTypeInfo();
        //    deviceType.Device_API = deviceData.DeviceAPI;
        //    deviceType.Device_Type = deviceData.HSDeviceType;

        //    device.set_DeviceType_Set(HS, deviceType);

        //    device.set_Last_Change(HS, DateTime.Now);

        //    if (!string.IsNullOrEmpty(deviceData.ScaleDisplayText))
        //    {
        //        device.set_ScaleText(HS, deviceData.ScaleDisplayText);
        //    }

        //    device.MISC_Set(HS, Enums.dvMISC.SHOW_VALUES);
        //    if (deviceData.StatusDevice)
        //    {
        //        device.MISC_Set(HS, Enums.dvMISC.STATUS_ONLY);
        //        device.MISC_Clear(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
        //        device.MISC_Clear(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
        //        device.set_Status_Support(HS, true);
        //    }
        //    else
        //    {
        //        device.MISC_Set(HS, Enums.dvMISC.SET_DOES_NOT_CHANGE_LAST_CHANGE);
        //        device.MISC_Set(HS, Enums.dvMISC.AUTO_VOICE_COMMAND);
        //        device.set_Status_Support(HS, false);
        //    }

        //    var pairs = deviceData.StatusPairs;
        //    foreach (var pair in pairs)
        //    {
        //        HS.DeviceVSP_AddPair(refId, pair);
        //    }

        //    var gPairs = deviceData.GraphicsPairs;
        //    foreach (var gpair in gPairs)
        //    {
        //        HS.DeviceVGP_AddPair(refId, gpair);
        //    }

        //    DeviceClass parent = null;
        //    if (optionalParentRefId.HasValue)
        //    {
        //        parent = (DeviceClass)HS.GetDeviceByRef(optionalParentRefId.Value);
        //    }

        //    if (parent != null)
        //    {
        //        parent.set_Relationship(HS, Enums.eRelationship.Parent_Root);
        //        device.set_Relationship(HS, Enums.eRelationship.Child);
        //        device.AssociatedDevice_Add(HS, parent.get_Ref(HS));
        //        parent.AssociatedDevice_Add(HS, device.get_Ref(HS));
        //    }

        //    deviceData.SetInitialData(HS, refId);
        //}

        // return device;

        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Creates the devices based on configuration.
        /// </summary>
        //private void CreateDevices(PlugInDevices hsDevices)
        //{
        //    try
        //    {
        //        var existingDevices = hsDevices.Children.ToDictionary(x => x.Value.Data.Id);
        //        foreach (var deviceImport in importDevicesData)
        //        {
        //            combinedToken.Token.ThrowIfCancellationRequested();

        //            if (!existingDevices.TryGetValue(deviceImport.Key, out var device))
        //            {
        //                DeviceIdentifier deviceIdentifier = new DeviceIdentifier(deviceImport.Value.Id);
        //                // lazy creation of parent device when child is created
        //                if (!hsDevices.ParentRefId.HasValue)
        //                {
        //                    var parentDeviceClass = CreateDevice(null, Invariant($"{PlugInData.PlugInName} Root"),
        //                                                         deviceIdentifier.RootDeviceAddress, new RootDeviceData());
        //                    hsDevices.ParentRefId = parentDeviceClass.Ref;
        //                }

        //                string address = deviceIdentifier.Address;
        //                var childDevice = new NumberDeviceData(deviceImport.Value);

        private void MigrateDevices()
        {
            var migrator = new HS3DeviceMigrator(HS, this.cancellationToken);
            migrator.Migrate();
        }

        //                var childHSDevice = CreateDevice(hsDevices.ParentRefId.Value, deviceImport.Value.Name, address, childDevice);
        //                hsDevices.Children[childHSDevice.Ref] = childDevice;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to Create Devices For PlugIn With {ex.GetFullMessage()}"));
        //    }
        //}
        private void StartDeviceFetchFromDB()
        {
            using (var sync = collectionTasksLock.Lock())
            {
                foreach (var childDeviceKeyValuePair in ImportDevices)
                {
                    int refID = childDeviceKeyValuePair.Key;
                    DeviceData deviceData = childDeviceKeyValuePair.Value;

                    this.combinedToken.Token.ThrowIfCancellationRequested();
                    collectionTasks.Add(ImportDataForDeviceInLoop(refID, deviceData));
                }
            }
        }

        private readonly CancellationToken cancellationToken;
        private readonly IReadOnlyDictionary<int, DeviceData> ImportDevices;
        private readonly List<Task> collectionTasks = new List<Task>();
        private readonly AsyncLock collectionTasksLock = new AsyncLock();
        private readonly CancellationTokenSource combinedToken;
        private readonly InfluxDBLoginInformation dbLoginInformation;
        private readonly IHsController HS;
        private bool disposedValue; // To detect redundant calls
    };
}