namespace Contract.DTOs.Response.Payment;

public class CreatePaymentLinkResponse
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
}
