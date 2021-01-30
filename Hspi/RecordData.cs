using System;

namespace Hspi
{
    internal sealed record RecordData
    {
        public RecordData(int deviceRefId, in double deviceValue, string deviceString,
                          string name, string location1, string location2, in DateTime timeStamp)
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
        public DateTime TimeStamp { get; }
    }
}