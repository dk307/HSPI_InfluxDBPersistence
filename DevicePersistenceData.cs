using NullGuard;
using System.Collections.Generic;

namespace Hspi
{

    public enum TrackedType
    {
        Value = 0,
#pragma warning disable CA1720 // Identifier contains type name
        String = 1,
#pragma warning restore CA1720 // Identifier contains type name
    }

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    public sealed class DevicePersistenceData
    {
        public DevicePersistenceData(string id, int deviceRefId, string measurement,
                                     [AllowNull]string field = null, [AllowNull]string fieldString = null,
                                     [AllowNull]IReadOnlyDictionary<string, string> tags = null,
                                     [AllowNull]double? maxValidValue = null, [AllowNull]double? minValidValue = null,
                                     [AllowNull]TrackedType? trackedType = null)
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
        public string Id { get; }
        public string Measurement { get; }
        public string Field { get; }
        public string FieldString { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public double? MaxValidValue { get; }
        public double? MinValidValue { get; }
        public TrackedType TrackedType { get; }
    }
}