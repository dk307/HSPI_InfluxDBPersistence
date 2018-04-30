using HomeSeerAPI;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Hspi
{
    using System.Diagnostics;
    using static System.FormattableString;

    internal class DevicePersistenceData
    {
        public DevicePersistenceData(string id, int deviceRefId, string measurement, string field, [AllowNull]IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            DeviceRefId = deviceRefId;
            Measurement = measurement;
            Field = field;
            Tags = tags;
        }

        public int DeviceRefId { get; }
        public string Id { get; }
        public string Measurement { get; }
        public string Field { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
    }

    internal class InfluxDBLoginInformation
    {
        public InfluxDBLoginInformation(System.Uri dBUri, [AllowNull]string user, [AllowNull]string password, string db)
        {
            DBUri = dBUri;
            User = user;
            Password = password;
            DB = db;
        }

        public string DB { get; }
        public System.Uri DBUri { get; }
        public string Password { get; }
        public string User { get; }
    }

    /// <summary>
    /// Class to store PlugIn Configuration
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal class PluginConfig : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfig"/> class.
        /// </summary>
        /// <param name="HS">The homeseer application.</param>
        public PluginConfig(IHSApplication HS)
        {
            this.HS = HS;

            // read db uri
            System.Uri influxDBUri;
            string influxDBUriString = GetValue(InfluxDBUriKey, string.Empty);

            Uri.TryCreate(influxDBUriString, UriKind.Absolute, out influxDBUri);

            this.influxDBLoginInformation = new InfluxDBLoginInformation(
                influxDBUri,
                GetValue(InfluxDBUsernameKey, string.Empty),
                GetValue(InfluxDBPasswordKey, string.Empty),
                GetValue(InfluxDBDBKey, string.Empty)
             );

            string deviceIdsConcatString = GetValue(PersistenceIdsKey, string.Empty);
            var persistenceIds = deviceIdsConcatString.Split(PersistenceIdsSeparator);

            foreach (var persistenceId in persistenceIds)
            {
                string deviceRefIdString = GetValue(DeviceRefIdKey, string.Empty, persistenceId);

                if (!int.TryParse(deviceRefIdString, out int deviceRefId))
                {
                    continue;
                }

                string measurement = GetValue(MeasurementKey, string.Empty, persistenceId);
                string field = GetValue(FieldKey, string.Empty, persistenceId);

                var tagString = GetValue(TagsKey, string.Empty, persistenceId);

                Dictionary<string, string> tags = null;
                try
                {
                    tags = ObjectSerialize.DeSerializeToObject(tagString) as Dictionary<string, string>;
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"Failed to load tags for {deviceRefIdString} with {ex.GetFullMessage()}"));
                }

                this.devicePersistenceData.Add(persistenceId,
                                               new Hspi.DevicePersistenceData(persistenceId, deviceRefId, measurement, field, tags));
            }

            debugLogging = GetValue(DebugLoggingKey, false);
        }

        public event EventHandler<EventArgs> ConfigChanged;

        public InfluxDBLoginInformation DBLoginInformation
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return influxDBLoginInformation;
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }

            set
            {
                configLock.EnterWriteLock();
                try
                {
                    influxDBLoginInformation = value;
                    SetValue(InfluxDBUriKey, value.DBUri);
                    SetValue(InfluxDBUsernameKey, value.User);
                    SetValue(InfluxDBPasswordKey, value.Password);
                    SetValue(InfluxDBDBKey, value.DB);
                }
                finally
                {
                    configLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug logging is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [debug logging]; otherwise, <c>false</c>.
        /// </value>
        public bool DebugLogging
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return debugLogging;
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }

            set
            {
                configLock.EnterWriteLock();
                try
                {
                    SetValue(DebugLoggingKey, value, ref debugLogging);
                }
                finally
                {
                    configLock.ExitWriteLock();
                }
            }
        }

        public IReadOnlyDictionary<string, DevicePersistenceData> DevicePersistenceData
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return new Dictionary<string, DevicePersistenceData>(devicePersistenceData);
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }
        }

        public void AddDevicePersistenceData(DevicePersistenceData device)
        {
            configLock.EnterWriteLock();
            try
            {
                devicePersistenceData[device.Id] = device;

                SetValue(DeviceRefIdKey, device.DeviceRefId, device.Id);
                SetValue(MeasurementKey, device.Measurement, device.Id);
                SetValue(FieldKey, device.Field, device.Id);
                SetValue(TagsKey, ObjectSerialize.SerializeToString(device.Tags) ?? string.Empty, device.Id);
                SetValue(PersistenceIdsKey, devicePersistenceData.Keys.Aggregate((x, y) => x + PersistenceIdsSeparator + y));
            }
            finally
            {
                configLock.ExitWriteLock();
            }
        }

        public void RemoveDevicePersistenceData(string id)
        {
            configLock.EnterWriteLock();
            try
            {
                devicePersistenceData.Remove(id);
                if (devicePersistenceData.Count > 0)
                {
                    SetValue(PersistenceIdsKey, devicePersistenceData.Keys.Aggregate((x, y) => x + PersistenceIdsSeparator + y));
                }
                else
                {
                    SetValue(PersistenceIdsKey, string.Empty);
                }
                HS.ClearINISection(id, FileName);
            }
            finally
            {
                configLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Fires event that configuration changed.
        /// </summary>
        public void FireConfigChanged()
        {
            if (ConfigChanged != null)
            {
                var ConfigChangedCopy = ConfigChanged;
                ConfigChangedCopy(this, EventArgs.Empty);
            }
        }

        private T GetValue<T>(string key, T defaultValue)
        {
            return GetValue(key, defaultValue, DefaultSection);
        }

        private T GetValue<T>(string key, T defaultValue, string section)
        {
            string stringValue = HS.GetINISetting(section, key, null, FileName);

            if (stringValue != null)
            {
                try
                {
                    T result = (T)System.Convert.ChangeType(stringValue, typeof(T), CultureInfo.InvariantCulture);
                    return result;
                }
                catch (Exception)
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private void SetValue<T>(string key, T value, string section = DefaultSection)
        {
            string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
            HS.SaveINISetting(section, key, stringValue, FileName);
        }

        private void SetValue<T>(string key, T value, ref T oldValue)
        {
            SetValue<T>(key, value, ref oldValue, DefaultSection);
        }

        private void SetValue<T>(string key, T value, ref T oldValue, string section)
        {
            if (!value.Equals(oldValue))
            {
                string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
                HS.SaveINISetting(section, key, stringValue, FileName);
                oldValue = value;
            }
        }

        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    configLock.Dispose();
                }
                disposedValue = true;
            }
        }

        #endregion IDisposable Support

        private const string DebugLoggingKey = "DebugLogging";
        private const string DefaultSection = "Settings";
        private const string InfluxDBDBKey = "InfluxDBDB";
        private const string InfluxDBPasswordKey = "InfluxDBPassword";
        private const string InfluxDBUriKey = "InfluxDBUri";
        private const string InfluxDBUsernameKey = "InfluxDBUsername";
        private const string DeviceRefIdKey = "DeviceRefId";
        private const string MeasurementKey = "Measurement";
        private const string FieldKey = "Field";
        private const string TagsKey = "Tags";
        private string PersistenceIdsKey = "PersistenceIds";
        private char PersistenceIdsSeparator = ',';
        private readonly static string FileName = Invariant($"{Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location)}.ini");
        private readonly ReaderWriterLockSlim configLock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, DevicePersistenceData> devicePersistenceData = new Dictionary<string, DevicePersistenceData>();
        private readonly IHSApplication HS;
        private bool debugLogging;
        private bool disposedValue = false;
        private InfluxDBLoginInformation influxDBLoginInformation;
    };
}