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

    [HttpGet("pricing")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDiaPricing(CancellationToken ct)
    {
        var packages = await _paymentService.GetDiaTopupPricingsAsync(ct);
        return Ok(packages);
    }

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

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] WebhookType webhookBody, CancellationToken ct = default)
    {
        _logger.LogInformation("Webhook received: {WebhookBody}", System.Text.Json.JsonSerializer.Serialize(webhookBody));
        var handledDia = await _paymentService.HandlePayOSWebhookAsync(webhookBody, ct);
        _logger.LogInformation("Webhook handled - dia:{Dia}", handledDia);
        return Ok(new { success = handledDia });
    }

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
