using Contract.DTOs.Request;
using Microsoft.AspNetCore.Mvc;
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
    {
        try
        {
            await _auth.SendRegisterOtpAsync(req, ct);
            return Ok(new { message = "OTP đã gửi. Vui lòng kiểm tra email." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = new { code = "BadRequest", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = new { code = "Conflict", message = ex.Message } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = new { code = "InternalError", message = "Lỗi hệ thống." } });
        }
    }
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var jwt = await _auth.VerifyRegisterAsync(req, ct);
        return Ok(new { token = jwt });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        try
        {
            var res = await _auth.LoginAsync(req, ct);
            return Ok(res); // { username, email, token }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        try
        {
            var res = await _auth.LoginWithGoogleAsync(req, ct);
            return Ok(res);
        }
        catch (InvalidOperationException ex) when (ex.Message == "AccountNotRegistered")
        {
            return Conflict(new
            {
                error = new
                {
                    code = "AccountNotRegistered",
                    message = "Tài khoản Google chưa liên kết. Vui lòng hoàn tất đăng ký."
                }
            });
        }
    }

    [HttpPost("google/complete")]
    public async Task<IActionResult> CompleteGoogleRegister([FromBody] CompleteGoogleRegisterRequest req, CancellationToken ct)
    {
        var res = await _auth.CompleteGoogleRegisterAsync(req, ct);
        return Ok(res); // { username, email, token }
    }
}
