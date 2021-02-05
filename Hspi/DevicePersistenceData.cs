using System.Collections.Generic;

#nullable enable

namespace Hspi
{
#pragma warning disable CA1720 // Identifier contains type name

    internal enum TrackedType
    {
        Value = 0,
        String = 1,
    }

#pragma warning restore CA1720 // Identifier contains type name

    internal sealed record DevicePersistenceData
    {
        public DevicePersistenceData(string id, int deviceRefId, string measurement,
                                     string? field = null, string? fieldString = null,
                                     IReadOnlyDictionary<string, string>? tags = null,
                                     double? maxValidValue = null, double? minValidValue = null,
                                     TrackedType? trackedType = null)
        {
            Id = id;
            DeviceRefId = deviceRefId;
            Measurement = measurement;
            Field = field;
            FieldString = fieldString;
            Tags = tags;
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
        public readonly IReadOnlyDictionary<string, string>? Tags;
        public readonly TrackedType TrackedType;
    }
}