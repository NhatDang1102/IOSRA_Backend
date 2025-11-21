using System;

namespace Contract.DTOs.Response.Subscription
{
    public class SubscriptionStatusResponse
    {
        public bool HasActiveSubscription { get; set; }
        public string? PlanCode { get; set; }
        public string? PlanName { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public uint DailyDias { get; set; }
        public ulong PriceVnd { get; set; }
        public DateTime? LastClaimedAt { get; set; }
        public bool CanClaimToday { get; set; }
    }
}
