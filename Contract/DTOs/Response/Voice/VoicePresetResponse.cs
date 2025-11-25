using System;

namespace Contract.DTOs.Response.Voice
{
    public class VoicePresetResponse
    {
        public Guid VoiceId { get; set; }
        public string VoiceName { get; set; } = string.Empty;
        public string VoiceCode { get; set; } = string.Empty;
        public string ProviderVoiceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
