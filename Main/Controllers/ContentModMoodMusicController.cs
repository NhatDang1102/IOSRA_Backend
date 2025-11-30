using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.ContentMod;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "cmod,CMOD,CONTENT_MODERATOR,content_moderator")]
    public class ContentModMoodMusicController : AppControllerBase
    {
        private readonly IMoodMusicService _service;

        public ContentModMoodMusicController(IMoodMusicService service)
        {
            _service = service;
        }

        [HttpGet("moods")]
        public async Task<IActionResult> GetMoods(CancellationToken ct)
        {
            var result = await _service.GetMoodsAsync(ct);
            return Ok(result);
        }

        [HttpGet("tracks")]
        public async Task<IActionResult> GetTracks([FromQuery] string? moodCode, CancellationToken ct)
        {
            var result = await _service.GetTracksAsync(moodCode, ct);
            return Ok(result);
        }

        [HttpPost("tracks")]
        public async Task<IActionResult> CreateTrack([FromBody] MoodTrackCreateRequest request, CancellationToken ct)
        {
            var result = await _service.CreateAsync(AccountId, request, ct);
            return Ok(result);
        }

        [HttpPut("tracks/{trackId:guid}")]
        public async Task<IActionResult> UpdateTrack(Guid trackId, [FromBody] MoodTrackUpdateRequest request, CancellationToken ct)
        {
            var result = await _service.UpdateAsync(trackId, request, ct);
            return Ok(result);
        }

        [HttpDelete("tracks/{trackId:guid}")]
        public async Task<IActionResult> DeleteTrack(Guid trackId, CancellationToken ct)
        {
            await _service.DeleteAsync(trackId, ct);
            return NoContent();
        }
    }
}
