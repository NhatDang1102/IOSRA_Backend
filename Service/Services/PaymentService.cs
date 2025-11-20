using Contract.DTOs.Response.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;
using Repository.DBContext;
using Repository.Entities;
using Service.Interfaces;

namespace Service.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly PayOS _payOS;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext db, PayOS payOS, ILogger<PaymentService> logger)
    {
        _db = db;
        _payOS = payOS;
        _logger = logger;
    }

    public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(Guid accountId, ulong amount)
    {
        var pricing = await _db.topup_pricings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.amount_vnd == amount && p.is_active);
        if (pricing == null)
        {
            throw new ArgumentException("Invalid top-up amount. Please select an available package.");
        }

        var diamondGranted = pricing.diamond_granted;

        var wallet = await _db.dia_wallets.FirstOrDefaultAsync(w => w.account_id == accountId);
        if (wallet == null)
        {
            wallet = new dia_wallet
            {
                wallet_id = Guid.NewGuid(),
                account_id = accountId,
                balance_coin = 0,
                locked_coin = 0,
                updated_at = DateTime.UtcNow
            };
            _db.dia_wallets.Add(wallet);
            await _db.SaveChangesAsync();
        }

        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var topupId = Guid.NewGuid();

        var item = new ItemData($"Nạp {diamondGranted} dias", 1, (int)amount);
        var items = new List<ItemData> { item };

        var baseUrl = "https://toranovel.id.vn";
        var paymentData = new PaymentData(
            orderCode,
            (int)amount,
            $"Nạp {diamondGranted} dias cho IOSRA",
            items,
            $"{baseUrl}/payment/cancel",
            $"{baseUrl}/payment/success"
        );

        var result = await _payOS.createPaymentLink(paymentData);

        var diaPayment = new dia_payment
        {
            topup_id = topupId,
            wallet_id = wallet.wallet_id,
            provider = "PayOS",
            order_code = orderCode.ToString(),
            amount_vnd = amount,
            diamond_granted = diamondGranted,
            status = "pending",
            created_at = DateTime.UtcNow
        };
        _db.dia_payments.Add(diaPayment);
        await _db.SaveChangesAsync();

        return new CreatePaymentLinkResponse
        {
            CheckoutUrl = result.checkoutUrl,
            TransactionId = orderCode.ToString()
        };
    }

    public async Task<bool> HandlePayOSWebhookAsync(WebhookType webhookBody)
    {
        WebhookData webhookData;
        try
        {
            _logger.LogInformation("Received webhook: {WebhookBody}", System.Text.Json.JsonSerializer.Serialize(webhookBody));
            webhookData = _payOS.verifyPaymentWebhookData(webhookBody);
            _logger.LogInformation("Verified webhook data: {WebhookData}", System.Text.Json.JsonSerializer.Serialize(webhookData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verify webhook failed");
            return false;
        }

        var orderCode = webhookData.orderCode.ToString();
        _logger.LogInformation("Looking for order_code = {OrderCode} in DB", orderCode);

        var diaPayment = await _db.dia_payments
            .Include(p => p.wallet)
            .FirstOrDefaultAsync(p => p.order_code == orderCode);

        if (diaPayment == null)
        {
            _logger.LogWarning("Transaction with order_code {OrderCode} not found in DB", orderCode);
            return false;
        }

        bool isPaid = webhookBody.success
                      && webhookData.code == "00"
                      && webhookData.desc?.Trim().Equals("success", StringComparison.OrdinalIgnoreCase) == true;

        _logger.LogInformation("Payment status: {Status}", isPaid ? "Success" : "Failed");
        diaPayment.status = isPaid ? "success" : "failed";

        if (isPaid)
        {
            try
            {
                var wallet = diaPayment.wallet;
                var oldBalance = wallet.balance_coin;
                var newBalance = oldBalance + (long)diaPayment.diamond_granted;
                wallet.balance_coin = newBalance;
                wallet.updated_at = DateTime.UtcNow;

                var walletPayment = new wallet_payment
                {
                    trs_id = Guid.NewGuid(),
                    wallet_id = wallet.wallet_id,
                    type = "topup",
                    coin_delta = (long)diaPayment.diamond_granted,
                    coin_after = newBalance,
                    ref_id = diaPayment.topup_id,
                    created_at = DateTime.UtcNow
                };
                _db.wallet_payments.Add(walletPayment);

                _db.payment_receipts.Add(new payment_receipt
                {
                    receipt_id = Guid.NewGuid(),
                    account_id = wallet.account_id,
                    ref_id = diaPayment.topup_id,
                    type = "dia_topup",
                    amount_vnd = diaPayment.amount_vnd,
                    created_at = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                _logger.LogInformation("Successfully topped up {Amount} dias to wallet {WalletId}", diaPayment.diamond_granted, wallet.wallet_id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update wallet");
                return false;
            }
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null)
    {
        if (!long.TryParse(transactionId, out var orderCode))
        {
            _logger.LogWarning("Invalid transaction ID format: {TransactionId}", transactionId);
            return false;
        }

        try
        {
            await _payOS.cancelPaymentLink(orderCode, cancellationReason ?? "User cancelled payment");
            _logger.LogInformation("Successfully cancelled payment link with order code: {OrderCode}", orderCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel payment link with order code: {OrderCode}", orderCode);
            return false;
        }
    }
}
