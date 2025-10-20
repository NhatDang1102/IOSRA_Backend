using Contract.DTOs.Request;
using Main.Models;
using Microsoft.AspNetCore.Mvc;
using Service.Exceptions;
using Service.Interfaces;

namespace Main.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
        => await ExecuteAsync(async () =>
        {
            await _auth.SendRegisterOtpAsync(req, ct);
            return Ok(new { message = "OTP sent. Please check your email." });
        });

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
        => await ExecuteAsync(async () =>
        {
            var jwt = await _auth.VerifyRegisterAsync(req, ct);
            return Ok(new { token = jwt });
        });

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
        => await ExecuteAsync(async () =>
        {
            var res = await _auth.LoginAsync(req, ct);
            return Ok(res);
        });

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
        => await ExecuteAsync(async () =>
        {
            var res = await _auth.LoginWithGoogleAsync(req, ct);
            return Ok(res);
        });

    [HttpPost("google/complete")]
    public async Task<IActionResult> CompleteGoogleRegister([FromBody] CompleteGoogleRegisterRequest req, CancellationToken ct)
        => await ExecuteAsync(async () =>
        {
            var res = await _auth.CompleteGoogleRegisterAsync(req, ct);
            return Ok(res);
        });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> handler)
    {
        try
        {
            return await handler();
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, ErrorResponse.From(ex.ErrorCode, ex.Message, ex.Details));
        }
        catch (Exception)
        {
            return StatusCode(500, ErrorResponse.From("InternalServerError", "An unexpected error occurred."));
        }
    }
}
