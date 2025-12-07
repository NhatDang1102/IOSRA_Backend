using System;

namespace Contract.DTOs.Response.Report
{
    public class ReportCommentDetailResponse
    {
        public Guid CommentId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid? StoryId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid ReaderId { get; set; }
        public string ReaderUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
