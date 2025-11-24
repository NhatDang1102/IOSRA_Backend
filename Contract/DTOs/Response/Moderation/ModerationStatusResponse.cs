using System;

namespace Contract.DTOs.Response.Moderation
{
    public class ModerationStatusResponse
    {
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string Status { get; set; } = null!;
    }
}
