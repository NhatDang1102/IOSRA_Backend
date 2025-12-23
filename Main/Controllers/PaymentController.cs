using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Net.payOS.Types;
using Service.Interfaces;

namespace Main.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : AppControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    // API Tạo link thanh toán nạp Kim cương (One-time payment)
    // Flow: Client gửi số tiền -> Server tạo link PayOS -> Trả về link cho Client redirect
    [HttpPost("create-link")]
    [Authorize]
    public async Task<IActionResult> CreateLink([FromBody] CreatePaymentLinkRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _paymentService.CreateTopupLinkAsync(AccountId, request.Amount, ct);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment link for account {AccountId}", AccountId);
            return StatusCode(500, new { message = "An error occurred while creating payment link" });
        }
    }

    // API Lấy danh sách các gói nạp Kim cương và giá tiền
    [HttpGet("pricing")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDiaPricing(CancellationToken ct)
    {
        var packages = await _paymentService.GetDiaTopupPricingsAsync(ct);
        return Ok(packages);
    }

    // API Tạo link thanh toán đăng ký gói Hội viên (Subscription)
    // Flow: Client chọn gói -> Server tạo link PayOS tương ứng -> Trả về link
    [HttpPost("create-subscription-link")]
    [Authorize]
    public async Task<IActionResult> CreateSubscriptionLink([FromBody] CreateSubscriptionPaymentLinkRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _paymentService.CreateSubscriptionLinkAsync(AccountId, request, ct);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription payment link for account {AccountId}", AccountId);
            return StatusCode(500, new { message = "An error occurred while creating subscription payment link" });
        }
    }

    // API Webhook nhận kết quả từ PayOS (Quan trọng)
    // PayOS sẽ gọi vào đây khi người dùng thanh toán thành công hoặc thất bại.
    // Server cần verify data, check signature và cập nhật trạng thái đơn hàng (Cộng tiền/Kích hoạt gói).
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] WebhookType webhookBody, CancellationToken ct = default)
    {
        _logger.LogInformation("Webhook received: {WebhookBody}", System.Text.Json.JsonSerializer.Serialize(webhookBody));
        var handledDia = await _paymentService.HandlePayOSWebhookAsync(webhookBody, ct);
        _logger.LogInformation("Webhook handled - dia:{Dia}", handledDia);
        return Ok(new { success = handledDia });
    }

    // API Hủy link thanh toán
    // Dùng khi người dùng bấm "Hủy" hoặc muốn tạo đơn mới khi đơn cũ chưa thanh toán xong.
    [HttpPost("cancel-link")]
    [Authorize]
    public async Task<IActionResult> CancelLink([FromBody] CancelPaymentRequest request, CancellationToken ct = default)
    {
        try
        {
            var success = await _paymentService.CancelPaymentLinkAsync(request.TransactionId, request.CancellationReason, ct);
            if (success)
            {
                return Ok(new { success = true, message = "Payment link cancelled successfully" });
            }
            return BadRequest(new { success = false, message = "Failed to cancel payment link" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment link for transaction {TransactionId}", request.TransactionId);
            return StatusCode(500, new { success = false, message = "An error occurred while cancelling payment link" });
        }
    }
}
