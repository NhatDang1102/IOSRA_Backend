using System;

namespace Contract.DTOs.Response.Subscription
{
    public class SubscriptionClaimResponse
    {
        public Guid SubscriptionId { get; set; }
        public uint ClaimedDias { get; set; }
        public long WalletBalance { get; set; }
        public DateTime ClaimedAt { get; set; }
        public DateTime? NextClaimAvailableAt { get; set; }
    }
}
