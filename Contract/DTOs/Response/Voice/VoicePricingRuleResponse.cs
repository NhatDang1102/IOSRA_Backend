namespace Contract.DTOs.Response.Voice
{
    public class VoicePricingRuleResponse
    {
        public int MinCharCount { get; set; }
        public int? MaxCharCount { get; set; }
        public int GenerationCostDias { get; set; }
        public int SellingPriceDias { get; set; }
    }
}
