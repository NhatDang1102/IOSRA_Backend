using System;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterCatalogVoiceResponse
    {
        public Guid VoiceId { get; set; }
        public string VoiceName { get; set; } = string.Empty;
        public string VoiceCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int PriceDias { get; set; }
        public bool HasAudio { get; set; }
        public bool Owned { get; set; }
        public string? AudioUrl { get; set; }
    }
}
