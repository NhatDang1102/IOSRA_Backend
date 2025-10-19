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
        await _auth.SendRegisterOtpAsync(req, ct);
        return Ok(new { message = "OTP đã gửi. Vui lòng kiểm tra email." });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var jwt = await _auth.VerifyRegisterAsync(req, ct);
        return Ok(new { token = jwt });
    }

    // NEW:
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
}
