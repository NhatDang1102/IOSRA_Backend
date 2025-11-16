using System;

namespace Contract.DTOs.Respond.Moderation
{
    public class ModerationStatusResponse
    {
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string Status { get; set; } = null!;
    }
}
