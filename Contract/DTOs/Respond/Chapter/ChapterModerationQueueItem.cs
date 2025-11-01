using System;

namespace Contract.DTOs.Respond.Chapter
{
    public class ChapterModerationQueueItem
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = null!;
        public string ChapterTitle { get; set; } = null!;
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = null!;
        public string AuthorEmail { get; set; } = null!;
        public int ChapterNo { get; set; }
        public int WordCount { get; set; }
        public string LanguageCode { get; set; } = null!;
        public string LanguageName { get; set; } = null!;
        public int PriceDias { get; set; }
        public decimal? AiScore { get; set; }
        public string? AiFeedback { get; set; }
        public string Status { get; set; } = null!;
        public DateTime SubmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
