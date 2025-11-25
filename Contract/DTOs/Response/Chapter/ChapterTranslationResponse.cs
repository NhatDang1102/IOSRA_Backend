using System;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterTranslationResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string OriginalLanguageCode { get; set; } = string.Empty;
        public string TargetLanguageCode { get; set; } = string.Empty;
        public string TargetLanguageName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ContentUrl { get; set; }
        public int WordCount { get; set; }
        public bool Cached { get; set; }
    }
}
