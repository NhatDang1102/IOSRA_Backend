using System.Collections.Generic;
using Contract.DTOs.Response.Common;

namespace Contract.DTOs.Response.OperationMod
{
    public class OperationRevenueResponse
    {
        public string Period { get; set; } = "month";
        public long DiaTopup { get; set; }
        public long Subscription { get; set; }
        public long VoiceTopup { get; set; }
        public List<StatPointResponse> Points { get; set; } = new();
    }
}
