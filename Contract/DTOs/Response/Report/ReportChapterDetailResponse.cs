using System;

namespace Contract.DTOs.Response.Report
{
    public class ReportChapterDetailResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ChapterNo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AccessType { get; set; } = string.Empty;
        public uint PriceDias { get; set; }
        public string? ContentPath { get; set; }
        public string? LanguageCode { get; set; }
        public string? LanguageName { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = string.Empty;
    }
}
