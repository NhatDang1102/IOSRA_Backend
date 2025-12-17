using System;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Admin
{
    public class UpdateChapterPriceRuleRequest
    {
        [Required]
        public Guid RuleId { get; set; }
        [Required]
        public uint DiasPrice { get; set; }
    }

    public class UpdateVoicePriceRuleRequest
    {
        [Required]
        public Guid RuleId { get; set; }
        [Required]
        public uint DiasPrice { get; set; }
        [Required]
        public uint GenerationDias { get; set; }
    }

    public class UpdateTopupPricingRequest
    {
        [Required]
        public Guid PricingId { get; set; }
        [Required]
        public ulong DiamondGranted { get; set; }
    }

    public class UpdateSubscriptionPlanRequest
    {
        [Required]
        public string PlanCode { get; set; } = null!;
        [Required]
        public ulong PriceVnd { get; set; }
        [Required]
        public uint DailyDias { get; set; }
    }
}
