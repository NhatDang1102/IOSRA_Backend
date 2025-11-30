using System;

namespace Repository.DataModels
{
    public class StatPointData
    {
        public string Label { get; set; } = string.Empty;
        public DateTime RangeStart { get; set; }
        public DateTime RangeEnd { get; set; }
        public long Value { get; set; }
    }
}
