using System;

namespace Service.Models
{
    public readonly record struct VoiceSynthesisJob(Guid ChapterId, Guid VoiceId);
}
