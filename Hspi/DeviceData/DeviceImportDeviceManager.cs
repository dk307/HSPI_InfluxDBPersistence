using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class DeviceImportDeviceManager : IDisposable
    {
        public DeviceImportDeviceManager(IHsController HS,
                                         InfluxDBLoginInformation dbLoginInformation,
                                         CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.dbLoginInformation = dbLoginInformation;
            this.cancellationToken = cancellationToken;
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            MigrateDevices();

            importDevices = GetCurrentDevices().ToImmutableDictionary();
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

        public bool HasDevice(int refId)
        {
            return importDevices.ContainsKey(refId);
        }

        public async Task<bool> ImportDataForDevice(int refId)
        {
            if (importDevices.TryGetValue(refId, out var data))
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
                combinedToken.Token.ThrowIfCancellationRequested();
                try
                {
                    ERelationship relationship = (ERelationship)HS.GetPropertyByRef(refId, EProperty.Relationship);

                    //data is stored in feature(child)
                    if (relationship == ERelationship.Feature)
                    {
                        var deviceType = HSDeviceHelper.GetDeviceTypeFromPlugInData(HS, refId);

                        if (deviceType == DeviceImportDevice.DeviceType)
                        {
                            var importDevice = new DeviceImportDevice(HS, refId);
                            currentChildDevices.Add(refId, importDevice);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(Invariant($"{HSDeviceHelper.GetName(HS, refId)} has invalid plugin data load failed with {ex.GetFullMessage()}. Please recreate it."));
                }
            }

            return currentChildDevices;
        }

        private async ValueTask<ImportDeviceData?> ImportDataForDevice(DeviceImportDevice deviceData)
        {
            //start as task to fetch data
            ImportDeviceData? importDeviceData = null;
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
                    logger.Debug(Invariant($"Not importing from Db for {deviceData.Name} as it is deleted"));
                }
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to get value from Db for {deviceData.Name} with {ex.GetFullMessage()}"));
            }

            try
            {
                logger.Debug(Invariant($"Updating {deviceData.Name} with {deviceValue}"));
                deviceData.Update(deviceValue);
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to write value to HS for {deviceData.Name} with {ex.GetFullMessage()}"));
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
            using var sync = collectionTasksLock.Lock();
            foreach (var childDeviceKeyValuePair in importDevices)
            {
                DeviceImportDevice deviceData = childDeviceKeyValuePair.Value;

                this.combinedToken.Token.ThrowIfCancellationRequested();
                collectionTasks.Add(ImportDataForDeviceInLoop(deviceData));
            }
        }

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly CancellationToken cancellationToken;
        private readonly List<Task> collectionTasks = new List<Task>();
        private readonly AsyncLock collectionTasksLock = new AsyncLock();
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly CancellationTokenSource combinedToken;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly InfluxDBLoginInformation dbLoginInformation;
        private readonly IHsController HS;
        private readonly ImmutableDictionary<int, DeviceImportDevice> importDevices;
        private bool disposedValue;
    };
}