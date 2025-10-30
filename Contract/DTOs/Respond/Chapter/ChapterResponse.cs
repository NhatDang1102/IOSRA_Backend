using System;

namespace Contract.DTOs.Respond.Chapter
{
    public class ChapterResponse
    {
        public ulong ChapterId { get; set; }
        public ulong StoryId { get; set; }
        public int ChapterNo { get; set; }
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public int WordCount { get; set; }
        public string LanguageCode { get; set; } = null!;
        public string LanguageName { get; set; } = null!;
        public int PriceDias { get; set; }
        public string AccessType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal? AiScore { get; set; }
        public string? AiFeedback { get; set; }
        public string? ContentPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
}
