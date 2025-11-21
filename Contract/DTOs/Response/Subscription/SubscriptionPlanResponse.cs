using System;

namespace Contract.DTOs.Response.Subscription
{
    public class SubscriptionPlanResponse
    {
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public ulong PriceVnd { get; set; }
        public uint DurationDays { get; set; }
        public uint DailyDias { get; set; }
    }
}
