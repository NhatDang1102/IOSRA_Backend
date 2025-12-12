using Contract.DTOs.Response.Subscription;

namespace Contract.DTOs.Response.Profile
{
    public class ProfileWalletResponse
    {
        public long DiaBalance { get; set; }
        public bool IsAuthor { get; set; }
        public SubscriptionStatusResponse? Subscription { get; set; }
    }
}
