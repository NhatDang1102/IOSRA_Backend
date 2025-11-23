using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Respond.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize]
    [Route("api/ChapterPurchase")]
    public class ChapterPurchaseController : AppControllerBase
    {
        private readonly IChapterPurchaseService _chapterPurchaseService;

        public ChapterPurchaseController(IChapterPurchaseService chapterPurchaseService)
        {
            _chapterPurchaseService = chapterPurchaseService;
        }

        [HttpPost("{chapterId:guid}")]
        public async Task<ActionResult<ChapterPurchaseResponse>> Purchase(Guid chapterId, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.PurchaseAsync(AccountId, chapterId, ct);
            return Ok(result);
        }
    }
}
