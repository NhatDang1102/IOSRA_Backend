using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Payment;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;
using Repository.Entities;
using Repository.Utils;
using Repository.Interfaces;
using Service.Interfaces;

namespace Service.Services;

public class PaymentService : IPaymentService
{
        private readonly IBillingRepository _billingRepository;
        private readonly PayOS _payOS;
        private readonly ILogger<PaymentService> _logger;
        private readonly ISubscriptionService _subscriptionService;

        public PaymentService(
            IBillingRepository billingRepository,
            PayOS payOS,
            ILogger<PaymentService> logger,
            ISubscriptionService subscriptionService)
        {
            _billingRepository = billingRepository;
            _payOS = payOS;
            _logger = logger;
            _subscriptionService = subscriptionService;
        }

    public Task<CreatePaymentLinkResponse> CreateTopupLinkAsync(Guid accountId, ulong amount, CancellationToken ct = default)
        => CreateDiaTopupLinkAsync(accountId, amount, ct);

    public async Task<IReadOnlyList<DiaTopupPricingResponse>> GetDiaTopupPricingsAsync(CancellationToken ct = default)
    {
        var entries = await _billingRepository.GetDiaTopupPricingsAsync(ct);
        return entries
            .Select(MapDiaPricing)
            .ToArray();
    }

    public Task<CreatePaymentLinkResponse> CreateSubscriptionLinkAsync(Guid accountId, CreateSubscriptionPaymentLinkRequest request, CancellationToken ct = default)
        => CreateSubscriptionPaymentLinkAsync(accountId, request, ct);
    //tạo link payos từ amount (bóc trong db ra), tạo luôn ordercode rồi gọi api _payOS.createPaymentLink
    private async Task<CreatePaymentLinkResponse> CreateDiaTopupLinkAsync(Guid accountId, ulong amount, CancellationToken ct)
    {
        if (amount == 0)
        {
            throw new ArgumentException("Amount is required for dia top-up requests.");
        }

            var pricing = await _billingRepository.GetDiaTopupPricingAsync(amount, ct);
            if (pricing == null)
            {
                throw new ArgumentException("Invalid top-up amount. Please select an available package.");
            }

            var wallet = await _billingRepository.GetOrCreateDiaWalletAsync(accountId, ct);

        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var topupId = Guid.NewGuid();

        var description = $"Topup {pricing.diamond_granted} dias";
        var item = new ItemData(description, 1, (int)amount);
        var paymentData = BuildPaymentData(orderCode, (int)amount, description, item);

        var result = await _payOS.createPaymentLink(paymentData);

        var diaPayment = new dia_payment
        {
            topup_id = topupId,
            wallet_id = wallet.wallet_id,
            provider = "PayOS",
            order_code = orderCode.ToString(),
            amount_vnd = amount,
            diamond_granted = pricing.diamond_granted,
            status = "pending",
            created_at = TimezoneConverter.VietnamNow
        };
            await _billingRepository.AddDiaPaymentAsync(diaPayment, ct);
            await _billingRepository.SaveChangesAsync(ct);

        return new CreatePaymentLinkResponse
        {
            CheckoutUrl = result.checkoutUrl,
            TransactionId = orderCode.ToString()
        };
    }

        private async Task<CreatePaymentLinkResponse> CreateSubscriptionPaymentLinkAsync(Guid accountId, CreateSubscriptionPaymentLinkRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.PlanCode))
            {
                throw new ArgumentException("SubscriptionPlanCode is required for subscription purchases.");
            }

            var planCode = request.PlanCode.Trim();
            var plan = await _billingRepository.GetSubscriptionPlanAsync(planCode, ct)
                       ?? throw new ArgumentException("Subscription plan not found.");

        if (plan.price_vnd == 0)
        {
            throw new ArgumentException("Subscription plan is not configured with a price.");
        }

        if (plan.price_vnd > int.MaxValue)
        {
            throw new ArgumentException("Subscription price exceeds PayOS limit.");
        }

        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var description = $"Subscription {plan.plan_name}";
        var item = new ItemData(description, 1, (int)plan.price_vnd);
        var paymentData = BuildPaymentData(orderCode, (int)plan.price_vnd, description, item);

        var result = await _payOS.createPaymentLink(paymentData);

        var now = TimezoneConverter.VietnamNow;
        var record = new subscription_payment
        {
            payment_id = Guid.NewGuid(),
            account_id = accountId,
            plan_code = plan.plan_code,
            provider = "PayOS",
            order_code = orderCode.ToString(),
            amount_vnd = plan.price_vnd,
            status = "pending",
            created_at = now
        };
        await _billingRepository.AddSubscriptionPaymentAsync(record, ct);
        await _billingRepository.SaveChangesAsync(ct);

        return new CreatePaymentLinkResponse
        {
            CheckoutUrl = result.checkoutUrl,
            TransactionId = orderCode.ToString()
        };
    }
    //xác minh tính hợp lí của data webhook từ payos, nếu khớp thì gd thành công 
    public async Task<bool> HandlePayOSWebhookAsync(WebhookType webhookBody, CancellationToken ct = default)
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
        var isPaid = webhookBody.success
                      && webhookData.code == "00"
                      && webhookData.desc?.Trim().Equals("success", StringComparison.OrdinalIgnoreCase) == true;

        var handled = false;
        var now = TimezoneConverter.VietnamNow;

        var diaPayment = await _billingRepository.GetDiaPaymentByOrderCodeAsync(orderCode, ct);

        if (diaPayment != null)
        {
            handled = true;
            diaPayment.status = isPaid ? "success" : "failed";

            if (isPaid)
            {
                try
                {
                    var wallet = diaPayment.wallet;
                                var newBalance = wallet.balance_dias + (long)diaPayment.diamond_granted;
                                wallet.balance_dias = newBalance;                    
                    wallet.updated_at = now;

                    await _billingRepository.AddWalletPaymentAsync(new wallet_payment
                    {
                        trs_id = Guid.NewGuid(),
                        wallet_id = wallet.wallet_id,
                        type = "topup",
                                        dias_delta = (long)diaPayment.diamond_granted,
                                        dias_after = newBalance,                      
                        ref_id = diaPayment.topup_id,
                        created_at = now
                    });

                    await _billingRepository.AddPaymentReceiptAsync(new payment_receipt
                    {
                        receipt_id = Guid.NewGuid(),
                        account_id = wallet.account_id,
                        ref_id = diaPayment.topup_id,
                        type = "dia_topup",
                        amount_vnd = diaPayment.amount_vnd,
                        created_at = now
                    });

                    await _billingRepository.SaveChangesAsync(ct);
                    _logger.LogInformation("Successfully topped up {Amount} dias to wallet {WalletId}", diaPayment.diamond_granted, wallet.wallet_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update wallet for dia topup");
                    return false;
                }
            }
            else
            {
                await _billingRepository.SaveChangesAsync(ct);
            }
        }

        var subPayment = await _billingRepository.GetSubscriptionPaymentByOrderCodeAsync(orderCode, ct);
        if (subPayment != null)
        {
            handled = true;
            subPayment.status = isPaid ? "success" : "failed";
            if (isPaid)
            {
                await _subscriptionService.ActivateSubscriptionAsync(subPayment.account_id, subPayment.plan_code, ct);
                await _billingRepository.AddPaymentReceiptAsync(new payment_receipt
                {
                    receipt_id = Guid.NewGuid(),
                    account_id = subPayment.account_id,
                    ref_id = subPayment.payment_id,
                    type = "subscription",
                    amount_vnd = subPayment.amount_vnd,
                    created_at = now
                });
                await _billingRepository.SaveChangesAsync(ct);
            }
            else
            {
                await _billingRepository.SaveChangesAsync(ct);
            }
        }

        if (!handled)
        {
            _logger.LogWarning("No payment record matched order {OrderCode}", orderCode);
        }

        return handled;
    }

    public async Task<bool> CancelPaymentLinkAsync(string transactionId, string? cancellationReason = null, CancellationToken ct = default)
    {
        if (!long.TryParse(transactionId, out var orderCode))
        {
            _logger.LogWarning("Invalid transaction ID format: {TransactionId}", transactionId);
            return false;
        }

        var diaPayment = await _billingRepository.GetDiaPaymentByOrderCodeAsync(transactionId, ct);
        var subPayment = await _billingRepository.GetSubscriptionPaymentByOrderCodeAsync(transactionId, ct);

        // Cố gắng gọi PayOS để hủy, nhưng không chặn việc cập nhật DB nếu thất bại
        try
        {
            await _payOS.cancelPaymentLink(orderCode, cancellationReason ?? "User cancelled payment");
            _logger.LogInformation("Successfully cancelled payment link with order code: {OrderCode}", orderCode);
        }
        catch (Exception ex)
        {
            // Thường lỗi do link đã được hủy trên UI PayOS trước đó, ta chỉ log lại
            _logger.LogWarning("PayOS cancel API returned error (possibly already cancelled): {Message}", ex.Message);
        }

        var updated = false;
        if (diaPayment != null && string.Equals(diaPayment.status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            diaPayment.status = "cancelled";
            updated = true;
        }

        if (subPayment != null && string.Equals(subPayment.status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            subPayment.status = "cancelled";
            updated = true;
        }

        if (updated)
        {
            await _billingRepository.SaveChangesAsync(ct);
            _logger.LogInformation("Local payment record marked as cancelled for order {OrderCode}", transactionId);
        }
        else if (diaPayment == null && subPayment == null)
        {
            _logger.LogWarning("No payment record found for order {OrderCode}", transactionId);
            return false;
        }

        return true;
    }

    private static DiaTopupPricingResponse MapDiaPricing(topup_pricing entity)
        => new DiaTopupPricingResponse
        {
            PricingId = entity.pricing_id,
            AmountVnd = entity.amount_vnd,
            DiamondGranted = entity.diamond_granted,
            IsActive = entity.is_active,
            UpdatedAt = entity.updated_at
        };

    private static PaymentData BuildPaymentData(long orderCode, int amount, string description, ItemData item)
    {
        const string BaseUrl = "https://iosra-web.vercel.app";
        var normalizedDescription = NormalizeDescription(description);
        return new PaymentData(
            orderCode,
            amount,
            normalizedDescription,
            new List<ItemData> { item },
            $"{BaseUrl}/payment/cancel",
            $"{BaseUrl}/payment/success");
    }

    private static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "IOSRA Payment";
        }

        var trimmed = description.Trim();
        return trimmed.Length <= 25 ? trimmed : trimmed[..25];
    }
}

