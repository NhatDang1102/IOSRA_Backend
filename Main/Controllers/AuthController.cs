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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        await _authService.SendRegisterOtpAsync(req, ct);
        return Ok(new { message = "OTP sent. Please check your email." });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var res = await _authService.VerifyRegisterAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var res = await _authService.LoginAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        var res = await _authService.LoginWithGoogleAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    [HttpPost("google/complete")]
    public async Task<IActionResult> CompleteGoogleRegister([FromBody] CompleteGoogleRegisterRequest req, CancellationToken ct)
    {
        var res = await _authService.CompleteGoogleRegisterAsync(req, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshSession(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            return Unauthorized(new { error = new { code = "RefreshTokenMissing", message = "Refresh token is missing." } });
        }

        var validation = await _refreshTokenStore.ValidateAsync(refreshToken, ct);
        if (validation == null)
        {
            await ClearRefreshCookieAsync(refreshToken, ct);
            return Unauthorized(new { error = new { code = "RefreshTokenInvalid", message = "Refresh token is invalid or expired." } });
        }

        await _refreshTokenStore.RevokeAsync(refreshToken, ct);

        var res = await _authService.RefreshAsync(validation.AccountId, ct);
        await IssueRefreshCookieAsync(res.AccountId, ct);
        return Ok(res);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
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

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        await _blacklistService.BlacklistAsync(jti, expiresAt, ct);
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

    private async Task IssueRefreshCookieAsync(Guid accountId, CancellationToken ct)
    {
        var issued = await _refreshTokenStore.IssueAsync(accountId, ct);
        Response.Cookies.Append(RefreshCookieName, issued.Token, BuildCookieOptions(issued.ExpiresAt));
    }

    private async Task ClearRefreshCookieAsync(string refreshToken, CancellationToken ct)
    {
        await _refreshTokenStore.RevokeAsync(refreshToken, ct);
        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTime.UtcNow.AddYears(-1)));
    }

    private static CookieOptions BuildCookieOptions(DateTime expiresUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc))
        };
    }
}
