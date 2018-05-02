using NullGuard;
using System.Collections.Generic;

namespace Hspi
{
    internal class DevicePersistenceData
    {
        public DevicePersistenceData(string id, int deviceRefId, string measurement,
                                     [AllowNull]string field, [AllowNull]string fieldString, 
                                     [AllowNull]IReadOnlyDictionary<string, string> tags,
                                     [AllowNull]double? maxValidValue, [AllowNull]double? minValidValue)
        {
            Id = id;
            DeviceRefId = deviceRefId;
            Measurement = measurement;
            Field = field;
            FieldString = fieldString;
            Tags = tags;
            MaxValidValue = maxValidValue;
            MinValidValue = minValidValue;
        }

        public int DeviceRefId { get; }
        public string Id { get; }
        public string Measurement { get; }
        public string Field { get; }
        public string FieldString { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public double? MaxValidValue { get; }
        public double? MinValidValue { get; }
    }
}