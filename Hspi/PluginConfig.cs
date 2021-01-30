using HomeSeer.PluginSdk;
using Hspi.Utils;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    /// <summary>
    /// Class to store PlugIn Configuration
    /// </summary>
    internal sealed class PluginConfig : PluginConfigBase
    {
        public PluginConfig(IHsController HS) : base(HS)
        {
            this.influxDBLoginInformation = LoadDBSettings();
            this.devicePersistenceData = LoadPersistenceSettings();
        }

        [DisallowNull]

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
                if (!value.IsValid)
                {
                    throw new ArgumentException("DB Information is not valid");
                }

                using (var sync = configLock.WriterLock())
                {
                    influxDBLoginInformation = value;
                    SetValue(InfluxDBUriKey, value.DBUri);
                    SetValue(InfluxDBUsernameKey, value.User);
                    SetValue(InfluxDBPasswordKey, EncryptString(value.Password));
                    SetValue(InfluxDBDBKey, value.DB);
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

        public static string? CheckEmptyOrWhitespace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public void AddDevicePersistenceData(in DevicePersistenceData device)
        {
            if (string.IsNullOrWhiteSpace(device.Id))
            {
                throw new ArgumentException("device id is empty");
            }

            using (var sync = configLock.WriterLock())
            {
                var newdevicePersistenceData = new Dictionary<string, DevicePersistenceData>(devicePersistenceData);
                newdevicePersistenceData[device.Id] = device;
                devicePersistenceData = newdevicePersistenceData;

                SetValue(DeviceRefIdKey, device.DeviceRefId, device.Id);
                SetValue(MeasurementKey, device.Measurement, device.Id);
                SetValue(FieldKey, device.Field ?? string.Empty, device.Id);
                SetValue(FieldStringKey, device.FieldString ?? string.Empty, device.Id);
                SetValue(TagsKey, device.Tags != null ? ObjectSerialize.SerializeToString(device.Tags) : null, device.Id);
                SetValue(PersistenceIdsKey, devicePersistenceData.Keys.Aggregate((x, y) => x + PersistenceIdsSeparator + y));
                SetValue(MaxValidValueKey, device.MaxValidValue, device.Id);
                SetValue(MinValidValueKey, device.MinValidValue, device.Id);
                SetValue(TrackedTypeKey, device.TrackedType, device.Id);
            }
        }

        public void RemoveDevicePersistenceData(string id)
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

            ClearSection(id);
        }

        private InfluxDBLoginInformation LoadDBSettings()
        {
            // read db uri
            string influxDBUriString = GetValue(InfluxDBUriKey, string.Empty);

            Uri.TryCreate(influxDBUriString, UriKind.Absolute, out Uri influxDBUri);

            return new InfluxDBLoginInformation(
                influxDBUri,
                CheckEmptyOrWhitespace(GetValue(InfluxDBUsernameKey, string.Empty)),
                CheckEmptyOrWhitespace(DecryptString(GetValue(InfluxDBPasswordKey, string.Empty))),
                CheckEmptyOrWhitespace(GetValue(InfluxDBDBKey, string.Empty))
             );
        }

        private Dictionary<string, DevicePersistenceData> LoadPersistenceSettings()
        {
            string deviceIdsConcatString = GetValue(PersistenceIdsKey, string.Empty);
            var persistenceIds = deviceIdsConcatString.Split(PersistenceIdsSeparator);

            var persistData = new Dictionary<string, DevicePersistenceData>();
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
                string trackedTypeString = GetValue(TrackedTypeKey, string.Empty, persistenceId);

                var tagString = GetValue(TagsKey, string.Empty, persistenceId);

                Dictionary<string, string>? tags = null;
                try
                {
                    tags = ObjectSerialize.DeSerializeToObject(tagString) as Dictionary<string, string>;
                }
                catch (Exception ex)
                {
                    logger.Warn(Invariant($"Failed to load tags for {deviceRefIdString} with {ex.GetFullMessage()}"));
                }

                double? maxValidValue = null;
                double? minValidValue = null;
                TrackedType? trackedType = null;

                if (double.TryParse(maxValidValueString, out var value))
                {
                    maxValidValue = value;
                }

                if (double.TryParse(minValidValueString, out value))
                {
                    minValidValue = value;
                }

                if (Enum.TryParse<TrackedType>(trackedTypeString, out var trackedTypeValue))
                {
                    trackedType = trackedTypeValue;
                }

                var data = new DevicePersistenceData(persistenceId, deviceRefId, measurement, field, fieldString, tags,
                                                     maxValidValue, minValidValue, trackedType);
                persistData.Add(persistenceId, data);
            }

            return persistData;
        }

        public const string DefaultFieldValueString = "value";
        public const string DeviceLocation1Tag = "location1";
        public const string DeviceLocation2Tag = "location2";
        public const string DeviceNameTag = "name";
        public const string DeviceRefIdTag = "refid";
        public const string DeviceStringValueDefaultField = "valueString";
        public const string DeviceValueDefaultField = "value";
        private const string DeviceRefIdKey = "DeviceRefId";
        private const string FieldKey = "Field";
        private const string FieldStringKey = "FieldString";
        private const string InfluxDBDBKey = "InfluxDBDB";
        private const string InfluxDBPasswordKey = "InfluxDBPassword";
        private const string InfluxDBUriKey = "InfluxDBUri";
        private const string InfluxDBUsernameKey = "InfluxDBUsername";
        private const string MaxValidValueKey = "MaxValidValue";
        private const string MeasurementKey = "Measurement";
        private const string MinValidValueKey = "MinValidValue";
        private const string PersistenceIdsKey = "PersistenceIds";
        private const char PersistenceIdsSeparator = ',';
        private const string TagsKey = "Tags";
        private const string TrackedTypeKey = "TrackedTyp";

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly AsyncReaderWriterLock configLock = new AsyncReaderWriterLock();

        private Dictionary<string, DevicePersistenceData> devicePersistenceData;
        private InfluxDBLoginInformation influxDBLoginInformation;
    };
}