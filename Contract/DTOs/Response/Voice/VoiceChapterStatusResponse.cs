using System;

namespace Contract.DTOs.Response.Voice
{
    public class VoiceChapterStatusResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public int CharCount { get; set; }
        public int GenerationCostPerVoiceDias { get; set; }
        public VoiceChapterVoiceResponse[] Voices { get; set; } = Array.Empty<VoiceChapterVoiceResponse>();
    }
}
