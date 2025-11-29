using System;

namespace Contract.DTOs.Response.Chapter
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
        public bool IsOwned { get; set; }
        public int PriceDias { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string ContentUrl { get; set; } = null!;
        public PurchasedVoiceResponse[] Voices { get; set; } = Array.Empty<PurchasedVoiceResponse>();
    }
}
