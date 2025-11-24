using System.Collections.Generic;

namespace Contract.DTOs.Response.Common
{
    public class StatSeriesResponse
    {
        public string Period { get; set; } = "month";
        public long Total { get; set; }
        public List<StatPointResponse> Points { get; set; } = new();
    }
}
