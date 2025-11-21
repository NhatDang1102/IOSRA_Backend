using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment;

public class CreatePaymentLinkRequest
{
    [Required]
    public ulong Amount { get; set; }
}
