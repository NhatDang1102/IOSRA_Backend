using System;

namespace Contract.DTOs.Request.Report
{
    public class ReportCreateRequest
    {
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string Reason { get; set; } = null!;
        public string? Details { get; set; }
    }
}
