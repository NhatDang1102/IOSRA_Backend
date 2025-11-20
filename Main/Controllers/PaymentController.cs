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
    private readonly IVoicePaymentService _voicePaymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        IVoicePaymentService voicePaymentService,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _voicePaymentService = voicePaymentService;
        _logger = logger;
    }

    [HttpPost("create-link")]
    [Authorize]
    public async Task<IActionResult> CreateLink([FromBody] CreatePaymentLinkRequest request)
    {
        try
        {
            var response = await _paymentService.CreatePaymentLinkAsync(AccountId, request.Amount);
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

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] WebhookType webhookBody)
    {
        _logger.LogInformation("Webhook received: {WebhookBody}", System.Text.Json.JsonSerializer.Serialize(webhookBody));
        var handledDia = await _paymentService.HandlePayOSWebhookAsync(webhookBody);
        var handledVoice = await _voicePaymentService.HandleWebhookAsync(webhookBody);
        var handled = handledDia || handledVoice;
        _logger.LogInformation("Webhook handled - dia:{Dia} voice:{Voice}", handledDia, handledVoice);
        return Ok(new { success = handled });
    }

    [HttpPost("cancel-link")]
    [Authorize]
    public async Task<IActionResult> CancelLink([FromBody] CancelPaymentRequest request)
    {
        try
        {
            var success = await _paymentService.CancelPaymentLinkAsync(request.TransactionId, request.CancellationReason);
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
