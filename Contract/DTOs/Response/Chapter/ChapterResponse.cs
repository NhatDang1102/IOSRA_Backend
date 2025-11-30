using System;
using Contract.DTOs.Response.Voice;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public int ChapterNo { get; set; }
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public int WordCount { get; set; }
        public int CharCount { get; set; }
        public string LanguageCode { get; set; } = null!;
        public string LanguageName { get; set; } = null!;
        public int PriceDias { get; set; }
        public string AccessType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal? AiScore { get; set; }
        public string? AiFeedback { get; set; }
        public string? AiResult { get; set; }
        public string? ModeratorStatus { get; set; }
        public string? ModeratorNote { get; set; }
        public string? ContentPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public ChapterMoodResponse? Mood { get; set; }
        public VoiceChapterVoiceResponse[] Voices { get; set; } = Array.Empty<VoiceChapterVoiceResponse>();
    }
}
