namespace Contract.DTOs.Response.Voice
{
    public class VoiceChapterOrderResponse : VoiceChapterStatusResponse
    {
        public long TotalGenerationCostDias { get; set; }
        public long AuthorRevenueBalanceAfter { get; set; }
    }
}
