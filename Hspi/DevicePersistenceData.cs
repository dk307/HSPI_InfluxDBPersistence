using System.Collections.Generic;

#nullable enable

namespace Hspi
{
#pragma warning disable CA1720 // Identifier contains type name

    public enum TrackedType
    {
        Value = 0,
        String = 1,
    }

#pragma warning restore CA1720 // Identifier contains type name

    public sealed record DevicePersistenceData
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

        public int DeviceRefId { get; }
        public string? Field { get; }
        public string? FieldString { get; }
        public string Id { get; }
        public double? MaxValidValue { get; }
        public string Measurement { get; }
        public double? MinValidValue { get; }
        public IReadOnlyDictionary<string, string>? Tags { get; }
        public TrackedType TrackedType { get; }
    }
}