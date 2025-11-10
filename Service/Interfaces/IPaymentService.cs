using Contract.DTOs.Response.Payment;
using Net.payOS.Types;

namespace Service.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(Guid accountId, ulong amount);
    Task<bool> HandlePayOSWebhookAsync(WebhookType webhookBody);
    Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null);
}
