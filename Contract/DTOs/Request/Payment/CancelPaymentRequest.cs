using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment;

public class CancelPaymentRequest
{
    [Required(ErrorMessage = "Transaction ID is required")]
    public string TransactionId { get; set; } = string.Empty;

    public string? CancellationReason { get; set; }
}
