using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;
using Repository.DBContext;
using Repository.Entities;
using Service.Interfaces;

namespace Service.Services
{
    public class VoicePaymentService : IVoicePaymentService
    {
        private readonly AppDbContext _db;
        private readonly PayOS _payOS;
        private readonly ILogger<VoicePaymentService> _logger;

        public VoicePaymentService(AppDbContext db, PayOS payOS, ILogger<VoicePaymentService> logger)
        {
            _db = db;
            _payOS = payOS;
            _logger = logger;
        }

        public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(Guid accountId, ulong amount, CancellationToken ct = default)
        {
            var pricing = await _db.voice_topup_pricings
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.amount_vnd == amount && p.is_active, ct);
            if (pricing == null)
            {
                throw new ArgumentException("Invalid top-up amount. Please select an available package.");
            }

            var wallet = await _db.voice_wallets.FirstOrDefaultAsync(w => w.account_id == accountId, ct);
            if (wallet == null)
            {
                wallet = new voice_wallet
                {
                    wallet_id = Guid.NewGuid(),
                    account_id = accountId,
                    balance_chars = 0,
                    updated_at = DateTime.UtcNow
                };
                _db.voice_wallets.Add(wallet);
                await _db.SaveChangesAsync(ct);
            }

            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var topupId = Guid.NewGuid();

            var item = new ItemData($"Voice {pricing.chars_granted} chars", 1, (int)amount);
            var paymentData = new PaymentData(
                orderCode,
                (int)amount,
                $"Voice topup {pricing.chars_granted} chars",
                new List<ItemData> { item },
                "https://toranovel.id.vn/voice-payment/cancel",
                "https://toranovel.id.vn/voice-payment/success"
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
                created_at = DateTime.UtcNow
            };
            _db.voice_payments.Add(record);
            await _db.SaveChangesAsync(ct);

            return new CreatePaymentLinkResponse
            {
                CheckoutUrl = result.checkoutUrl,
                TransactionId = orderCode.ToString()
            };
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
            var record = await _db.voice_payments
                .Include(t => t.voice_wallet).ThenInclude(vw => vw.account)
                .FirstOrDefaultAsync(t => t.order_code == orderCode, ct);

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
                    wallet.updated_at = DateTime.UtcNow;
                    var newBalance = wallet.balance_chars;
                    _db.payment_receipts.Add(new payment_receipt
                    {
                        receipt_id = Guid.NewGuid(),
                        account_id = wallet.account.account_id,
                        ref_id = record.topup_id,
                        type = "voice_topup",
                        amount_vnd = record.amount_vnd,
                        created_at = DateTime.UtcNow
                    });
                    _db.voice_wallet_payments.Add(new voice_wallet_payment
                    {
                        trs_id = Guid.NewGuid(),
                        wallet_id = wallet.wallet_id,
                        type = "topup",
                        char_delta = (long)record.chars_granted,
                        char_after = newBalance,
                        ref_id = record.topup_id,
                        created_at = DateTime.UtcNow,
                        note = "Voice top-up via PayOS"
                    });
                    await _db.SaveChangesAsync(ct);
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
                await _db.SaveChangesAsync(ct);
            }

            return true;
        }

        public async Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null, CancellationToken ct = default)
        {
            if (!long.TryParse(transactionId, out var orderCode))
            {
                return false;
            }

            try
            {
                await _payOS.cancelPaymentLink(orderCode, cancellationReason ?? "User cancelled voice payment");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel voice payment link {OrderCode}", orderCode);
                return false;
            }
        }
    }
}
