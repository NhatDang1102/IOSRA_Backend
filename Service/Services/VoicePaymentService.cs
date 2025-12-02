using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Payment;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;
using Repository.Entities;
using Repository.Utils;
using Repository.Interfaces;
using Service.Interfaces;

namespace Service.Services
{
    public class VoicePaymentService : IVoicePaymentService
    {
        private readonly IBillingRepository _billingRepository;
        private readonly PayOS _payOS;
        private readonly ILogger<VoicePaymentService> _logger;

        public VoicePaymentService(IBillingRepository billingRepository, PayOS payOS, ILogger<VoicePaymentService> logger)
        {
            _billingRepository = billingRepository;
            _payOS = payOS;
            _logger = logger;
        }

        public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(Guid accountId, ulong amount, CancellationToken ct = default)
        {
            var pricing = await _billingRepository.GetVoiceTopupPricingAsync(amount, ct);
            if (pricing == null)
            {
                throw new ArgumentException("Invalid top-up amount. Please select an available package.");
            }

            var wallet = await _billingRepository.GetOrCreateVoiceWalletAsync(accountId, ct);

            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var topupId = Guid.NewGuid();

            var item = new ItemData($"Voice {pricing.chars_granted} chars", 1, (int)amount);
            var paymentData = new PaymentData(
                orderCode,
                (int)amount,
                $"Voice topup {pricing.chars_granted} chars",
                new List<ItemData> { item },
                "https://iosra-web.vercel.app/payment/cancel",
                "https://iosra-web.vercel.app/payment/success"
            );

            var result = await _payOS.createPaymentLink(paymentData);

            var record = new voice_payment
            {
                topup_id = topupId,
                wallet_id = wallet.wallet_id,
                provider = "PayOS",
                order_code = orderCode.ToString(),
                amount_vnd = amount,
                chars_granted = pricing.chars_granted,
                status = "pending",
                created_at = TimezoneConverter.VietnamNow
            };
            await _billingRepository.AddVoicePaymentAsync(record, ct);
            await _billingRepository.SaveChangesAsync(ct);

            return new CreatePaymentLinkResponse
            {
                CheckoutUrl = result.checkoutUrl,
                TransactionId = orderCode.ToString()
            };
        }

        public async Task<IReadOnlyList<VoiceTopupPricingResponse>> GetVoiceTopupPricingsAsync(CancellationToken ct = default)
        {
            var entries = await _billingRepository.GetVoiceTopupPricingsAsync(ct);
            return entries
                .Select(MapVoicePricing)
                .ToArray();
        }

        public async Task<bool> HandleWebhookAsync(WebhookType webhookBody, CancellationToken ct = default)
        {
            WebhookData webhookData;
            try
            {
                _logger.LogInformation("Voice webhook body: {Body}", System.Text.Json.JsonSerializer.Serialize(webhookBody));
                webhookData = _payOS.verifyPaymentWebhookData(webhookBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voice webhook verification failed");
                return false;
            }

            var orderCode = webhookData.orderCode.ToString();
            var record = await _billingRepository.GetVoicePaymentByOrderCodeAsync(orderCode, ct);

            if (record == null)
            {
                _logger.LogWarning("Voice topup with order code {OrderCode} not found", orderCode);
                return false;
            }

            bool isPaid = webhookBody.success
                          && webhookData.code == "00"
                          && webhookData.desc?.Trim().Equals("success", StringComparison.OrdinalIgnoreCase) == true;

            record.status = isPaid ? "success" : "failed";

            if (isPaid)
            {
                try
                {
                    var wallet = record.voice_wallet;
                    wallet.balance_chars += (long)record.chars_granted;
                    wallet.updated_at = TimezoneConverter.VietnamNow;
                    var newBalance = wallet.balance_chars;
                    await _billingRepository.AddPaymentReceiptAsync(new payment_receipt
                    {
                        receipt_id = Guid.NewGuid(),
                        account_id = wallet.account.account_id,
                        ref_id = record.topup_id,
                        type = "voice_topup",
                        amount_vnd = record.amount_vnd,
                        created_at = TimezoneConverter.VietnamNow
                    });
                    await _billingRepository.AddVoiceWalletPaymentAsync(new voice_wallet_payment
                    {
                        trs_id = Guid.NewGuid(),
                        wallet_id = wallet.wallet_id,
                        type = "topup",
                        char_delta = (long)record.chars_granted,
                        char_after = newBalance,
                        ref_id = record.topup_id,
                        created_at = TimezoneConverter.VietnamNow,
                        note = "Voice top-up via PayOS"
                    });
                    await _billingRepository.SaveChangesAsync(ct);
                    _logger.LogInformation("Voice wallet {WalletId} credited {Chars} chars", wallet.wallet_id, record.chars_granted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update voice wallet");
                    return false;
                }
            }
            else
            {
                await _billingRepository.SaveChangesAsync(ct);
            }

            return true;
        }

    public async Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null, CancellationToken ct = default)
    {
        if (!long.TryParse(transactionId, out var orderCode))
        {
            return false;
        }

        var record = await _billingRepository.GetVoicePaymentByOrderCodeAsync(transactionId, ct);

        try
        {
            await _payOS.cancelPaymentLink(orderCode, cancellationReason ?? "User cancelled voice payment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel voice payment link {OrderCode}", orderCode);
            return false;
        }

        if (record == null)
        {
            _logger.LogWarning("voice_payment record not found for order {OrderCode}", transactionId);
            return true;
        }

        if (string.Equals(record.status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            record.status = "cancelled";
            await _billingRepository.SaveChangesAsync(ct);
        }

        return true;
    }
        private static VoiceTopupPricingResponse MapVoicePricing(voice_topup_pricing entity)
            => new VoiceTopupPricingResponse
            {
                PricingId = entity.pricing_id,
                AmountVnd = entity.amount_vnd,
                CharsGranted = entity.chars_granted,
                IsActive = entity.is_active,
                UpdatedAt = entity.updated_at
            };
    }
}


