using System;

namespace Contract.DTOs.Response.Report
{
    public class ReportResponse
    {
        public Guid ReportId { get; set; }
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public Guid ReporterId { get; set; }
        public string ReporterUsername { get; set; } = null!;
        public Guid? ModeratorId { get; set; }
        public string? ModeratorUsername { get; set; }
        public string Reason { get; set; } = null!;
        public string? Details { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public ReportStoryDetailResponse? Story { get; set; }
        public ReportChapterDetailResponse? Chapter { get; set; }
        public ReportCommentDetailResponse? Comment { get; set; }
    }
}
