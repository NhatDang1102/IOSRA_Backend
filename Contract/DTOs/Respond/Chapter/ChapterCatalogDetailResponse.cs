using System;

namespace Contract.DTOs.Respond.Chapter
{
    public class ChapterCatalogDetailResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public int ChapterNo { get; set; }
        public string Title { get; set; } = null!;
        public string LanguageCode { get; set; } = null!;
        public int WordCount { get; set; }
        public string AccessType { get; set; } = null!;
        public bool IsLocked { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? Content { get; set; }
    }
}
