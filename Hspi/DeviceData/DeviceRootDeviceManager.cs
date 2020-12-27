using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
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

            ImportDevices = GetCurrentDevices();
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

        public async Task<bool> ImportDataForDevice(int refId)
        {
            if (ImportDevices.TryGetValue(refId, out var data))
            {
                await ImportDataForDevice(data).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        private Dictionary<int, DeviceImportDevice> GetCurrentDevices()
        {
            var refIds = HS.GetRefsByInterface(PlugInData.PlugInId);

            var currentChildDevices = new Dictionary<int, DeviceImportDevice>();

            foreach (var refId in refIds)
            {
                try
                {
                    ERelationship relationship = (ERelationship)HS.GetPropertyByRef(refId, EProperty.Relationship);

                    //data is stored in feature(child)
                    if (relationship == ERelationship.Feature)
                    {
                        currentChildDevices.Add(refId, new DeviceImportDevice(HS, refId));
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"{HSHelper.GetName(HS, refId)} has invalid plugin data load failed with {ex.GetFullMessage()}. Please recreate it."));
                }
            }

            return currentChildDevices;
        }

        private async Task<ImportDeviceData> ImportDataForDevice(DeviceImportDevice deviceData)
        {
            //start as task to fetch data
            ImportDeviceData importDeviceData = null;
            double? deviceValue = null;
            try
            {
                importDeviceData = deviceData.Data;
                if (importDeviceData != null)
                {
                    var queryData = await InfluxDBHelper.GetSingleValueForQuery(importDeviceData.Sql, dbLoginInformation).ConfigureAwait(false);
                    deviceValue = Convert.ToDouble(queryData, CultureInfo.InvariantCulture);
                }
                else
                {
                    Trace.WriteLine(Invariant($"Not importing from Db for {deviceData.Name} as it is deleted"));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to get value from Db for {deviceData.Name} with {ex.GetFullMessage()}"));
            }

            try
            {
                Trace.WriteLine(Invariant($"Updating {deviceData.Name} with {deviceValue}"));
                deviceData.Update(deviceValue);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to write value to HS for {deviceData.Name} with {ex.GetFullMessage()}"));
            }

            return importDeviceData;
        }

        private async Task ImportDataForDeviceInLoop(DeviceImportDevice deviceData)
        {
            while (!combinedToken.Token.IsCancellationRequested)
            {
                var importDeviceData = await ImportDataForDevice(deviceData).ConfigureAwait(false);
                if (importDeviceData != null)
                {
                    await Task.Delay((int)Math.Min(importDeviceData.IntervalSeconds * 1000, TimeSpan.FromDays(1).TotalMilliseconds),
                                     combinedToken.Token).ConfigureAwait(false);
                }
                else
                {
                    // device is not valid anymore
                    break;
                }
            }
        }

        private void MigrateDevices()
        {
            var migrator = new HS3DeviceMigrator(HS, this.cancellationToken);
            migrator.Migrate();
        }

        private void StartDeviceFetchFromDB()
        {
            using (var sync = collectionTasksLock.Lock())
            {
                foreach (var childDeviceKeyValuePair in ImportDevices)
                {
                    DeviceImportDevice deviceData = childDeviceKeyValuePair.Value;

                    this.combinedToken.Token.ThrowIfCancellationRequested();
                    collectionTasks.Add(ImportDataForDeviceInLoop(deviceData));
                }
            }
        }

        private readonly IReadOnlyDictionary<int, DeviceImportDevice> ImportDevices;
        private readonly CancellationToken cancellationToken;
        private readonly List<Task> collectionTasks = new List<Task>();
        private readonly AsyncLock collectionTasksLock = new AsyncLock();
        private readonly CancellationTokenSource combinedToken;
        private readonly InfluxDBLoginInformation dbLoginInformation;
        private readonly IHsController HS;
        private bool disposedValue;
    };
}