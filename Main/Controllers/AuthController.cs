using Contract.DTOs.Request.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace Main.Controllers;

[Route("api/[controller]")]
public class AuthController : AppControllerBase
{
    private readonly IAuthService _auth;
    private readonly IJwtBlacklistService _blacklist;

    public AuthController(IAuthService auth, IJwtBlacklistService blacklist)
    {
        _auth = auth;
        _blacklist = blacklist;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        await _auth.SendRegisterOtpAsync(req, ct);
        return Ok(new { message = "OTP sent. Please check your email." });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        // Trả LoginResponse (có AccountId, Token, Roles...)
        var res = await _auth.VerifyRegisterAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginWithGoogleAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("google/complete")]
    public async Task<IActionResult> CompleteGoogleRegister([FromBody] CompleteGoogleRegisterRequest req, CancellationToken ct)
    {
        var res = await _auth.CompleteGoogleRegisterAsync(req, ct);
        return Ok(res);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var expStr = User.FindFirst("exp")?.Value;

        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(expStr))
            return BadRequest(new { error = new { code = "InvalidToken", message = "Không tìm thấy thông tin JTI/EXP trong token." } });

        if (!long.TryParse(expStr, out var expUnix))
            return BadRequest(new { error = new { code = "InvalidToken", message = "EXP không hợp lệ." } });

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        await _blacklist.BlacklistAsync(jti, expiresAt, ct);

        return Ok(new { message = "Đăng xuất thành công." });
    }

    [HttpPost("forgot-pass")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await _auth.SendForgotOtpAsync(req, ct);
        return Ok(new { message = "Nếu email tồn tại, OTP đã được gửi." });
    }

    [HttpPost("forgot-pass/verify")]
    public async Task<IActionResult> VerifyForgotPassword([FromBody] VerifyForgotPasswordRequest req, CancellationToken ct)
    {
        await _auth.VerifyForgotAsync(req, ct);
        return Ok(new { message = "Đổi mật khẩu thành công." });
    }
}
