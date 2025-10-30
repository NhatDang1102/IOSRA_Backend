using System;

namespace Contract.DTOs.Respond.Chapter
{
    public class ChapterListItemResponse
    {
        public ulong ChapterId { get; set; }
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
    }
}
