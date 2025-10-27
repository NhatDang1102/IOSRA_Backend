using Contract.DTOs.Request.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers;

[Route("api/[controller]")]
[Authorize] // tất cả endpoint yêu cầu đăng nhập
public class ProfileController : AppControllerBase
{
    private readonly IProfileService _profile;

    public ProfileController(IProfileService profile)
    {
        _profile = profile;
    }

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var res = await _profile.GetAsync(AccountId, ct);
        return Ok(res);
    }

    // PUT /api/profile
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ProfileUpdateRequest req, CancellationToken ct)
    {
        var res = await _profile.UpdateAsync(AccountId, req, ct);
        return Ok(res);
    }

    // POST /api/profile/avatar (multipart/form-data)
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UpdateAvatar([FromForm] AvatarUploadRequest req, CancellationToken ct)
    {
        var url = await _profile.UpdateAvatarAsync(AccountId, req.File, ct);
        return Ok(new { avatarUrl = url });
    }

    // POST /api/profile/email/otp
    [HttpPost("email/otp")]
    public async Task<IActionResult> SendChangeEmailOtp([FromBody] ChangeEmailRequest req, CancellationToken ct)
    {
        await _profile.SendChangeEmailOtpAsync(AccountId, req, ct);
        return Ok(new { message = "Nếu email hợp lệ, OTP đã được gửi." });
    }

    // POST /api/profile/email/verify
    [HttpPost("email/verify")]
    public async Task<IActionResult> VerifyChangeEmail([FromBody] VerifyChangeEmailRequest req, CancellationToken ct)
    {
        await _profile.VerifyChangeEmailAsync(AccountId, req, ct);
        return Ok(new { message = "Đổi email thành công." });
    }
}
