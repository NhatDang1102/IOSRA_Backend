using Contract.DTOs.Request.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    private const string RefreshCookieName = "iosra_refresh";

    private readonly IAuthService _authService;
    private readonly IJwtBlacklistService _blacklistService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public AuthController(
        IAuthService authService,
        IJwtBlacklistService blacklistService,
        IRefreshTokenStore refreshTokenStore)
    {
        _authService = authService;
        _blacklistService = blacklistService;
        _refreshTokenStore = refreshTokenStore;
    }

    // API Đăng ký tài khoản (Bước 1: Gửi OTP)
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        await _authService.SendRegisterOtpAsync(req, ct);
        return Ok(new { message = "OTP sent. Please check your email." });
    }

    // API Xác thực OTP và hoàn tất đăng ký (Bước 2)
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var res = await _authService.VerifyRegisterAsync(req, ct);
        // Sau khi đăng ký thành công, cấp luôn Refresh Token qua Cookie để user đăng nhập luôn
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    // API Đăng nhập truyền thống (Username/Email + Password)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var res = await _authService.LoginAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    // API Đăng nhập bằng Google (Chỉ verify token)
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        var res = await _authService.LoginWithGoogleAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    // API Hoàn tất đăng ký bằng Google (Cung cấp thêm Username/Pass)
    [HttpPost("google/complete")]
    public async Task<IActionResult> CompleteGoogleRegister([FromBody] CompleteGoogleRegisterRequest req, CancellationToken ct)
    {
        var res = await _authService.CompleteGoogleRegisterAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    // API Làm mới Access Token (Refresh Token)
    // Client không cần gửi gì, Server tự lấy Refresh Token từ HttpOnly Cookie
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshSession(CancellationToken ct)
    {
        // 1. Lấy token từ Cookie (Bảo mật: Token không lộ ra Javascript)
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            return Unauthorized(new { error = new { code = "RefreshTokenMissing", message = "Refresh token is missing." } });
        }

        // 2. Validate token trong Redis (check xem có tồn tại và còn hạn không)
        var validation = await _refreshTokenStore.ValidateAsync(refreshToken, ct);
        if (validation == null)
        {
            // Nếu không hợp lệ -> Xóa cookie luôn để bắt đăng nhập lại
            await ClearRefreshCookieAsync(refreshToken, ct);
            return Unauthorized(new { error = new { code = "RefreshTokenInvalid", message = "Refresh token is invalid or expired." } });
        }

        // 3. Quan trọng: Xóa token cũ ngay lập tức (Token Rotation) để chống Replay Attack
        // Mỗi lần refresh là phải đổi token mới, ai dùng lại token cũ sẽ bị chặn
        await _refreshTokenStore.RevokeAsync(refreshToken, ct);

        // 4. Cấp token mới (Access + Refresh mới)
        var res = await _authService.RefreshAsync(validation.AccountId, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    // API Đăng xuất
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // 1. Lấy ID của Access Token hiện tại (JTI) từ Claims
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

        // 2. Đưa Access Token vào Blacklist trong Redis
        // Thời gian blacklist = Thời gian còn lại của token. Sau đó Redis tự xóa key.
        // Điều này đảm bảo dù có trộm được Access Token cũng không dùng được nữa.
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        await _blacklistService.BlacklistAsync(jti, expiresAt, ct);

        // 3. Xóa Refresh Token trong Cookie và xóa khỏi Redis
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            await ClearRefreshCookieAsync(refreshToken, ct);
        }

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("forgot-pass")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await _authService.SendForgotOtpAsync(req, ct);
        return Ok(new { message = "If the email exists, an OTP has been sent." });
    }

    [HttpPost("forgot-pass/verify")]
    public async Task<IActionResult> VerifyForgotPassword([FromBody] VerifyForgotPasswordRequest req, CancellationToken ct)
    {
        await _authService.VerifyForgotAsync(req, ct);
        return Ok(new { message = "Password updated successfully." });
    }

    // Helper: Cấp Refresh Token mới và lưu vào HttpOnly Cookie
    // HttpOnly giúp chống XSS (Javascript không đọc được token này)
    private async Task IssueRefreshCookieAsync(Guid accountId, CancellationToken ct)
    {
        var issued = await _refreshTokenStore.IssueAsync(accountId, ct);
        Response.Cookies.Append(RefreshCookieName, issued.Token, BuildCookieOptions(issued.ExpiresAt));
    }

    // Helper: Xóa Cookie và xóa token trong Redis
    private async Task ClearRefreshCookieAsync(string refreshToken, CancellationToken ct)
    {
        await _refreshTokenStore.RevokeAsync(refreshToken, ct);
        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTime.UtcNow.AddYears(-1)));
    }

    private static CookieOptions BuildCookieOptions(DateTime expiresUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,   // Quan trọng: Chặn Javascript đọc cookie (chống XSS)
            Secure = true,     // Chỉ gửi qua HTTPS
            SameSite = SameSiteMode.None, // Cho phép Cross-site (Cần thiết khi Frontend và Backend khác domain, ví dụ localhost:3000 gọi localhost:5000)
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc))
        };
    }
}