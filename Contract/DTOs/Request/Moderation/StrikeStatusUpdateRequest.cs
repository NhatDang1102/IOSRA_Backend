using System;

namespace Contract.DTOs.Request.Moderation
{
    public class StrikeStatusUpdateRequest
    {
        public byte Strike { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? RestrictedUntil { get; set; }
    }
}
