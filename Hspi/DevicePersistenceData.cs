using System.Collections.Generic;

#nullable enable

namespace Hspi
{
 
    internal enum TrackedType
    {
        Value = 0,
        String = 1,
    }

 
    internal sealed record DevicePersistenceData
    {
        public DevicePersistenceData(string id, int deviceRefId, string measurement,
                                     string? field = null, string? fieldString = null,
                                     double? maxValidValue = null, double? minValidValue = null,
                                     TrackedType? trackedType = null)
        {
            Id = id;
            DeviceRefId = deviceRefId;
            Measurement = measurement;
            Field = field;
            FieldString = fieldString;
            MaxValidValue = maxValidValue;
            MinValidValue = minValidValue;
            TrackedType = trackedType ?? TrackedType.Value;
        }

        public readonly int DeviceRefId;
        public readonly string? Field;
        public readonly string? FieldString;
        public readonly string Id;
        public readonly double? MaxValidValue;
        public readonly string Measurement;
        public readonly double? MinValidValue;
        public readonly TrackedType TrackedType;
    }
}