using System;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterTranslationStatusResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string OriginalLanguageCode { get; set; } = string.Empty;
        public ChapterTranslationLocaleStatus[] Locales { get; set; } = Array.Empty<ChapterTranslationLocaleStatus>();
    }

    public class ChapterTranslationLocaleStatus
    {
        public string LanguageCode { get; set; } = string.Empty;
        public string LanguageName { get; set; } = string.Empty;
        public bool IsOriginal { get; set; }
        public bool HasTranslation { get; set; }
        public string? ContentUrl { get; set; }
        public int WordCount { get; set; }
    }
}
