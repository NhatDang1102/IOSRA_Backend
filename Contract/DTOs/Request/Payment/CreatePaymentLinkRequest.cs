using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment;

public class CreatePaymentLinkRequest
{
    [Required(ErrorMessage = "Amount is required")]
    [Range(50000, 200000, ErrorMessage = "Amount must be 50000, 100000, or 200000")]
    public ulong Amount { get; set; }
}
