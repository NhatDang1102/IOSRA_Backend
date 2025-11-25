using System;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterListItemResponse
    {
        public Guid ChapterId { get; set; }
        public int ChapterNo { get; set; }
        public string Title { get; set; } = null!;
        public int WordCount { get; set; }
        public string LanguageCode { get; set; } = null!;
        public string LanguageName { get; set; } = null!;
        public int PriceDias { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public decimal? AiScore { get; set; }
        public string? AiResult { get; set; }
        public string? AiFeedback { get; set; }
        public string? ModeratorStatus { get; set; }
        public string? ModeratorFeedback { get; set; }
    }
}

