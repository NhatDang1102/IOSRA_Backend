using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment;

public class CreateSubscriptionPaymentLinkRequest
{
    [Required]
    [StringLength(32)]
    public string PlanCode { get; set; } = string.Empty;
}
