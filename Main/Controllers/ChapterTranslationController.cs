using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class ChapterTranslationController : AppControllerBase
    {
        private readonly IChapterTranslationService _translationService;

        public ChapterTranslationController(IChapterTranslationService translationService)
        {
            _translationService = translationService;
        }

        [HttpGet("{chapterId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ChapterTranslationResponse>> Get(Guid chapterId, [FromQuery] string languageCode, CancellationToken ct)
        {
            var response = await _translationService.GetAsync(chapterId, languageCode, TryGetAccountId(), ct);
            return Ok(response);
        }

        [HttpPost("{chapterId:guid}")]
        [Authorize]
        public async Task<ActionResult<ChapterTranslationResponse>> Translate(Guid chapterId, ChapterTranslationRequest request, CancellationToken ct)
        {
            var response = await _translationService.TranslateAsync(chapterId, request, AccountId, ct);
            return Ok(response);
        }
    }
}
