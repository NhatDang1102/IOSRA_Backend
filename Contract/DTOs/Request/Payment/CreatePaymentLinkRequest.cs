using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment;

public class CreatePaymentLinkRequest
{
    [Required(ErrorMessage = "Amount is required")]
    public ulong Amount { get; set; }
}
