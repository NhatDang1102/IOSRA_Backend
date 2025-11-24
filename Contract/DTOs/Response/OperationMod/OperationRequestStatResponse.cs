using System.Collections.Generic;
using Contract.DTOs.Response.Common;

namespace Contract.DTOs.Response.OperationMod
{
    public class OperationRequestStatResponse
    {
        public string Type { get; set; } = string.Empty;
        public string Period { get; set; } = "month";
        public long Total { get; set; }
        public List<StatPointResponse> Points { get; set; } = new();
    }
}
