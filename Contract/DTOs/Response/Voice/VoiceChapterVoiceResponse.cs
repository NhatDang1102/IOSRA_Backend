using System;

namespace Contract.DTOs.Response.Voice
{
    public class VoiceChapterVoiceResponse
    {
        public Guid VoiceId { get; set; }
        public string VoiceName { get; set; } = string.Empty;
        public string VoiceCode { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string? AudioUrl { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int CharCost { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
