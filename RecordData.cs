using System;
using NodaTime;

namespace Hspi
{
    internal class RecordData
    {
        public RecordData(int deviceRefId, double deviceValue, string deviceString, string name, string location1, string location2, Instant timeStamp)
        {
            this.DeviceRefId = deviceRefId;
            this.DeviceValue = deviceValue;
            this.DeviceString = deviceString;
            this.Name = name;
            this.Location1 = location1;
            this.Location2 = location2;
            this.TimeStamp = timeStamp;
        }

        public int DeviceRefId { get; }
        public double DeviceValue { get; }
        public string DeviceString { get; }
        public string Name { get; }
        public string Location1 { get; }
        public string Location2 { get; }
        public Instant TimeStamp { get; }
    }
}