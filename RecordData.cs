using System;

namespace Hspi
{
    internal class RecordData
    {
        public RecordData(int deviceRefId, double data, string name, string location1, string location2, DateTime timeStamp)
        {
            this.DeviceRefId = deviceRefId;
            this.Data = data;
            this.Name = name;
            this.Location1 = location1;
            this.Location2 = location2;
            this.TimeStamp = timeStamp;
        }

        public int DeviceRefId { get; }
        public double Data { get; }
        public string Name { get; }
        public string Location1 { get; }
        public string Location2 { get; }
        public DateTime TimeStamp { get; }
    }
}