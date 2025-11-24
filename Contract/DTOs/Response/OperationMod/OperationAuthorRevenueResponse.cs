using System.Collections.Generic;
using Contract.DTOs.Response.Common;

namespace Contract.DTOs.Response.OperationMod
{
    public class OperationAuthorRevenueResponse
    {
        public string Metric { get; set; } = "earned";
        public string Period { get; set; } = "month";
        public long Total { get; set; }
        public List<StatPointResponse> Points { get; set; } = new();
    }
}
