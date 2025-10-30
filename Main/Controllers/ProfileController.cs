using Contract.DTOs.Request.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Main.Controllers;

[Route("api/[controller]")]
[Authorize] // all endpoints require authentication
public class ProfileController : AppControllerBase
{
    private readonly IProfileService _profile;

    public ProfileController(IProfileService profile)
    {
        _profile = profile;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var res = await _profile.GetAsync(AccountId, ct);
        return Ok(res);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ProfileUpdateRequest req, CancellationToken ct)
    {
        var res = await _profile.UpdateAsync(AccountId, req, ct);
        return Ok(res);
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UpdateAvatar([FromForm] AvatarUploadRequest req, CancellationToken ct)
    {
        var url = await _profile.UpdateAvatarAsync(AccountId, req.File, ct);
        return Ok(new { avatarUrl = url });
    }

    [HttpPost("email/otp")]
    public async Task<IActionResult> SendChangeEmailOtp([FromBody] ChangeEmailRequest req, CancellationToken ct)
    {
        await _profile.SendChangeEmailOtpAsync(AccountId, req, ct);
        return Ok(new { message = "If the new email is valid, an OTP has been sent." });
    }

    [HttpPost("email/verify")]
    public async Task<IActionResult> VerifyChangeEmail([FromBody] VerifyChangeEmailRequest req, CancellationToken ct)
    {
        await _profile.VerifyChangeEmailAsync(AccountId, req, ct);
        return Ok(new { message = "Email updated successfully." });
    }
}
