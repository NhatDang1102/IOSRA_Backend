using System;

namespace Contract.DTOs.Response.Voice
{
    public class VoiceChapterCharCountResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
    }
}
