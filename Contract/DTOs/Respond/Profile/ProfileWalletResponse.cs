using Contract.DTOs.Response.Subscription;

namespace Contract.DTOs.Respond.Profile
{
    public class ProfileWalletResponse
    {
        public long DiaBalance { get; set; }
        public bool IsAuthor { get; set; }
        public long? VoiceCharBalance { get; set; }
        public SubscriptionStatusResponse Subscription { get; set; } = new SubscriptionStatusResponse
        {
            HasActiveSubscription = false,
            CanClaimToday = false
        };
    }
}
