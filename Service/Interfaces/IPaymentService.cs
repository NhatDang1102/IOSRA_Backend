using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Payment;
using Net.payOS.Types;

namespace Service.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentLinkResponse> CreateTopupLinkAsync(Guid accountId, ulong amount, CancellationToken ct = default);
    Task<IReadOnlyList<DiaTopupPricingResponse>> GetDiaTopupPricingsAsync(CancellationToken ct = default);
    Task<CreatePaymentLinkResponse> CreateSubscriptionLinkAsync(Guid accountId, CreateSubscriptionPaymentLinkRequest request, CancellationToken ct = default);
    Task<bool> HandlePayOSWebhookAsync(WebhookType webhookBody, CancellationToken ct = default);
    Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null, CancellationToken ct = default);
}
