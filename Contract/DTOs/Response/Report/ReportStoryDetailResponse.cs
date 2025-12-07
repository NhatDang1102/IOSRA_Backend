using System;

namespace Contract.DTOs.Response.Report
{
    public class ReportStoryDetailResponse
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string? LengthPlan { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = string.Empty;
    }
}
