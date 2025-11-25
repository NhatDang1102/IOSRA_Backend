using System;

namespace Contract.DTOs.Response.Chapter
{
    public class PurchasedVoiceHistoryResponse
    {
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = string.Empty;
        public PurchasedVoiceHistoryChapter[] Chapters { get; set; } = Array.Empty<PurchasedVoiceHistoryChapter>();
    }

    public class PurchasedVoiceHistoryChapter
    {
        public Guid ChapterId { get; set; }
        public int ChapterNo { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public PurchasedVoiceResponse[] Voices { get; set; } = Array.Empty<PurchasedVoiceResponse>();
    }
}
