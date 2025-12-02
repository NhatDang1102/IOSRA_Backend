using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Payment;
using Net.payOS.Types;

namespace Service.Interfaces
{
    public interface IVoicePaymentService
    {
        Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(Guid accountId, ulong amount, CancellationToken ct = default);
        Task<IReadOnlyList<VoiceTopupPricingResponse>> GetVoiceTopupPricingsAsync(CancellationToken ct = default);
        Task<bool> HandleWebhookAsync(WebhookType webhookBody, CancellationToken ct = default);
        Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null, CancellationToken ct = default);
    }
}
