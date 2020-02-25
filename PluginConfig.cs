using HomeSeerAPI;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using static System.FormattableString;

namespace Hspi
{
    /// <summary>
    /// Class to store PlugIn Configuration
    /// </summary>
    internal class PluginConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfig"/> class.
        /// </summary>
        /// <param name="HS">The homeseer application.</param>
        public PluginConfig(IHSApplication HS)
        {
            this.HS = HS;

            LoadDBSettings();
            LoadPersistenceSettings();
            LoadImportDeviceSettings();

            debugLogging = GetValue(DebugLoggingKey, false);
        }

        public event EventHandler<EventArgs> ConfigChanged;

        public InfluxDBLoginInformation DBLoginInformation
        {
            get
            {
                using (var sync = configLock.ReaderLock())
                {
                    return influxDBLoginInformation;
                }
            }

            set
            {
                using (var sync = configLock.WriterLock())
                {
                    influxDBLoginInformation = value;
                    SetValue(InfluxDBUriKey, value.DBUri);
                    SetValue(InfluxDBUsernameKey, value.User);
                    SetValue(InfluxDBPasswordKey, HS.EncryptString(value.Password, string.Empty));
                    SetValue(InfluxDBDBKey, value.DB);
                    SetValue(RetentionKey, value.Retention);
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
                using (var sync = configLock.ReaderLock())
                {
                    return debugLogging;
                }
            }

            set
            {
                using (var sync = configLock.WriterLock())
                {
                    SetValue(DebugLoggingKey, value, ref debugLogging);
                }
            }
        }

        public IImmutableDictionary<string, DevicePersistenceData> DevicePersistenceData
        {
            get
            {
                using (var sync = configLock.ReaderLock())
                {
                    return devicePersistenceData.ToImmutableDictionary();
                }
            }
        }

        public IImmutableDictionary<string, ImportDeviceData> ImportDevicesData
        {
            get
            {
                using (var sync = configLock.ReaderLock())
                {
                    return importDevicesData.ToImmutableDictionary();
                }
            }
        }

        public static string CheckEmptyOrWhitespace([AllowNull]string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public void AddDevicePersistenceData(in DevicePersistenceData device)
        {
            using (var sync = configLock.WriterLock())
            {
                var newdevicePersistenceData = new Dictionary<string, DevicePersistenceData>(devicePersistenceData);
                newdevicePersistenceData[device.Id] = device;
                devicePersistenceData = newdevicePersistenceData;

                SetValue(DeviceRefIdKey, device.DeviceRefId, device.Id);
                SetValue(MeasurementKey, device.Measurement, device.Id);
                SetValue(FieldKey, device.Field ?? string.Empty, device.Id);
                SetValue(FieldStringKey, device.FieldString ?? string.Empty, device.Id);
                SetValue(TagsKey, ObjectSerialize.SerializeToString(device.Tags) ?? string.Empty, device.Id);
                SetValue(PersistenceIdsKey, devicePersistenceData.Keys.Aggregate((x, y) => x + PersistenceIdsSeparator + y));
                SetValue(MaxValidValueKey, device.MaxValidValue, device.Id);
                SetValue(MinValidValueKey, device.MinValidValue, device.Id);
            }
        }

        public void AddImportDeviceData(in ImportDeviceData device)
        {
            using (var sync = configLock.WriterLock())
            {
                var newImportDeviceData = new Dictionary<string, ImportDeviceData>(importDevicesData);
                newImportDeviceData[device.Id] = device;
                importDevicesData = newImportDeviceData;

                SetValue(NameKey, device.Name, device.Id);
                SetValue(SqlKey, device.Sql, device.Id);
                SetValue(IntervalKey, device.Interval.TotalSeconds, device.Id);
                SetValue(UnitKey, device.Unit, device.Id);
                SetValue(ImportDevicesIdsKey, importDevicesData.Keys.Aggregate((x, y) => x + ImportDevicesIdsSeparator + y));
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

        public void RemoveDevicePersistenceData(string id)
        {
            using (var sync = configLock.WriterLock())
            {
                var newdevicePersistenceData = new Dictionary<string, DevicePersistenceData>(devicePersistenceData);
                newdevicePersistenceData.Remove(id);
                devicePersistenceData = newdevicePersistenceData;

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
        }

        public void RemoveImportDeviceData(string id)
        {
            using (var sync = configLock.WriterLock())
            {
                var newImportDeviceData = new Dictionary<string, ImportDeviceData>(importDevicesData);
                newImportDeviceData.Remove(id);
                importDevicesData = newImportDeviceData;

                if (importDevicesData.Count > 0)
                {
                    SetValue(ImportDevicesIdsKey, devicePersistenceData.Keys.Aggregate((x, y) => x + ImportDevicesIdsSeparator + y));
                }
                else
                {
                    SetValue(ImportDevicesIdsKey, string.Empty);
                }
                HS.ClearINISection(id, FileName);
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

        private void LoadDBSettings()
        {
            // read db uri
            string influxDBUriString = GetValue(InfluxDBUriKey, string.Empty);

            Uri.TryCreate(influxDBUriString, UriKind.Absolute, out Uri influxDBUri);

            this.influxDBLoginInformation = new InfluxDBLoginInformation(
                influxDBUri,
                CheckEmptyOrWhitespace(GetValue(InfluxDBUsernameKey, string.Empty)),
                CheckEmptyOrWhitespace(HS.DecryptString(GetValue(InfluxDBPasswordKey, string.Empty), string.Empty)),
                CheckEmptyOrWhitespace(GetValue(InfluxDBDBKey, string.Empty)),
                CheckEmptyOrWhitespace(GetValue(RetentionKey, string.Empty))
             );
        }

        private void LoadImportDeviceSettings()
        {
            string importDevicesConcatString = GetValue(ImportDevicesIdsKey, string.Empty);
            var ids = importDevicesConcatString.Split(ImportDevicesIdsSeparator);

            importDevicesData = new Dictionary<string, ImportDeviceData>();
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
                this.importDevicesData.Add(id, data);
            }
        }

        private void LoadPersistenceSettings()
        {
            string deviceIdsConcatString = GetValue(PersistenceIdsKey, string.Empty);
            var persistenceIds = deviceIdsConcatString.Split(PersistenceIdsSeparator);

            devicePersistenceData = new Dictionary<string, DevicePersistenceData>();
            foreach (var persistenceId in persistenceIds)
            {
                string deviceRefIdString = GetValue(DeviceRefIdKey, string.Empty, persistenceId);

                if (!int.TryParse(deviceRefIdString, out int deviceRefId))
                {
                    continue;
                }

                string measurement = GetValue(MeasurementKey, string.Empty, persistenceId);
                string field = GetValue(FieldKey, string.Empty, persistenceId);
                string fieldString = GetValue(FieldStringKey, string.Empty, persistenceId);
                string maxValidValueString = GetValue(MaxValidValueKey, string.Empty, persistenceId);
                string minValidValueString = GetValue(MinValidValueKey, string.Empty, persistenceId);

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

                double? maxValidValue = null;
                double? minValidValue = null;

                if (double.TryParse(maxValidValueString, out var value))
                {
                    maxValidValue = value;
                }

                if (double.TryParse(minValidValueString, out value))
                {
                    minValidValue = value;
                }

                var data = new DevicePersistenceData(persistenceId, deviceRefId, measurement, field, fieldString, tags, maxValidValue, minValidValue);
                this.devicePersistenceData.Add(persistenceId, data);
            }
        }

        private void SetValue<T>(string key, T value, string section = DefaultSection)
        {
            string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
            HS.SaveINISetting(section, key, stringValue, FileName);
        }

        private void SetValue<T>(string key, Nullable<T> value, string section = DefaultSection) where T : struct
        {
            string stringValue = value.HasValue ? System.Convert.ToString(value.Value, CultureInfo.InvariantCulture) : string.Empty;
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

        public const string DefaultFieldValueString = "value";
        public const string DeviceLocation1Tag = "location1";
        public const string DeviceLocation2Tag = "location2";
        public const string DeviceNameTag = "name";
        public const string DeviceRefIdTag = "refid";
        public const string DeviceStringValueDefaultField = "valueString";
        public const string DeviceValueDefaultField = "value";

        private const string DebugLoggingKey = "DebugLogging";
        private const string DefaultSection = "Settings";
        private const string DeviceRefIdKey = "DeviceRefId";
        private const string FieldKey = "Field";
        private const string FieldStringKey = "FieldString";
        private const string ImportDevicesIdsKey = "ImportDeviceIds";
        private const char ImportDevicesIdsSeparator = ',';
        private const string InfluxDBDBKey = "InfluxDBDB";
        private const string InfluxDBPasswordKey = "InfluxDBPassword";
        private const string InfluxDBUriKey = "InfluxDBUri";
        private const string InfluxDBUsernameKey = "InfluxDBUsername";
        private const string IntervalKey = "IntervalSeconds";
        private const string MaxValidValueKey = "MaxValidValue";
        private const string MeasurementKey = "Measurement";
        private const string MinValidValueKey = "MinValidValue";
        private const string NameKey = "Name";
        private const string PersistenceIdsKey = "PersistenceIds";
        private const char PersistenceIdsSeparator = ',';
        private const string RetentionKey = "Retention";
        private const string SqlKey = "Sql";
        private const string TagsKey = "Tags";
        private const string UnitKey = "Unit";
        private readonly static string FileName = Invariant($"{Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location)}.ini");
        private readonly AsyncReaderWriterLock configLock = new AsyncReaderWriterLock();
        private readonly IHSApplication HS;
        private bool debugLogging;
        private Dictionary<string, DevicePersistenceData> devicePersistenceData;
        private Dictionary<string, ImportDeviceData> importDevicesData;
        private InfluxDBLoginInformation influxDBLoginInformation;
    };
}