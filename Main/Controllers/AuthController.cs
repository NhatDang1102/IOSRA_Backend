using Contract.DTOs.Request.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Main.Controllers;

// Controller xử lý các chức năng xác thực: đăng ký, đăng nhập, đăng xuất, quên mật khẩu
[Route("api/[controller]")]
public class AuthController : AppControllerBase
{
    private readonly IAuthService _auth; // Service xử lý logic nghiệp vụ
    private readonly IJwtBlacklistService _blacklist; // Quản lý danh sách đen JWT

    public AuthController(IAuthService auth, IJwtBlacklistService blacklist)
    {
        _auth = auth;
        _blacklist = blacklist;
    }

    // Bước 1 đăng ký: Gửi mã OTP qua email
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        await _auth.SendRegisterOtpAsync(req, ct);
        return Ok(new { message = "OTP sent. Please check your email." });
    }

    // Bước 2 đăng ký: Xác thực OTP và tạo tài khoản, trả về JWT token
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var res = await _auth.VerifyRegisterAsync(req, ct);
        return Ok(res);
    }

    // Đăng nhập bằng email/username + password
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(req, ct);
        return Ok(res);
    }

    // Đăng nhập bằng Google - nếu chưa có tài khoản thì yêu cầu complete registration
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginWithGoogleAsync(req, ct);
        return Ok(res);
    }

    // Hoàn tất đăng ký cho tài khoản Google mới (tạo username + password)
    [HttpPost("google/complete")]
    public async Task<IActionResult> CompleteGoogleRegister([FromBody] CompleteGoogleRegisterRequest req, CancellationToken ct)
    {
        var res = await _auth.CompleteGoogleRegisterAsync(req, ct);
        return Ok(res);
    }

    // Đăng xuất - thêm JWT token hiện tại vào blacklist để vô hiệu hóa
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // Lấy JTI (JWT ID) và thời gian hết hạn từ token
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var expStr = User.FindFirst("exp")?.Value;

        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(expStr))
        {
            return BadRequest(new { error = new { code = "InvalidToken", message = "Token is missing JTI or EXP claims." } });
        }

        if (!long.TryParse(expStr, out var expUnix))
        {
            return BadRequest(new { error = new { code = "InvalidToken", message = "EXP claim is not a valid unix timestamp." } });
        }

        // Thêm token vào blacklist với TTL bằng thời gian còn lại đến khi hết hạn
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        await _blacklist.BlacklistAsync(jti, expiresAt, ct);

        return Ok(new { message = "Logged out successfully." });
    }

    // Bước 1 quên mật khẩu: Gửi OTP qua email (luôn trả về success để tránh lộ thông tin)
    [HttpPost("forgot-pass")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await _auth.SendForgotOtpAsync(req, ct);
        return Ok(new { message = "If the email exists, an OTP has been sent." });
    }

    // Bước 2 quên mật khẩu: Xác thực OTP và cập nhật mật khẩu mới
    [HttpPost("forgot-pass/verify")]
    public async Task<IActionResult> VerifyForgotPassword([FromBody] VerifyForgotPasswordRequest req, CancellationToken ct)
    {
        await _auth.VerifyForgotAsync(req, ct);
        return Ok(new { message = "Password updated successfully." });
    }
}
