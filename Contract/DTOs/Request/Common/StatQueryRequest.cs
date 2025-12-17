using System;

namespace Contract.DTOs.Request.Common
{
    public class StatQueryRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Period { get; set; }
        public bool GenerateReport { get; set; } = false;
    }
}
