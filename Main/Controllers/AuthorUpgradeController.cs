using Contract.DTOs.Request.Author;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class AuthorUpgradeController : AppControllerBase
    {
        private readonly IAuthorUpgradeService _authorUpgrade;
        private readonly IAuthorRankPromotionService _rankPromotion;

        public AuthorUpgradeController(IAuthorUpgradeService authorUpgrade, IAuthorRankPromotionService rankPromotion)
        {
            _authorUpgrade = authorUpgrade;
            _rankPromotion = rankPromotion;
        }

        // Reader submits an upgrade request to become an author (Casual rank)
        [Authorize(Roles = "reader,READER")]
        [HttpPost("request")]
        public async Task<IActionResult> Submit([FromBody] SubmitAuthorUpgradeRequest req, CancellationToken ct)
        {
            var res = await _authorUpgrade.SubmitAsync(AccountId, req, ct);
            return Ok(res);
        }

        // Reader/Author retrieves their own upgrade requests
        [Authorize(Roles = "reader,READER,author,AUTHOR")]
        [HttpGet("my-requests")]
        public async Task<IActionResult> MyRequests(CancellationToken ct)
        {
            var res = await _authorUpgrade.ListMyRequestsAsync(AccountId, ct);
            return Ok(res);
        }

        [Authorize(Roles = "author,AUTHOR")]
        [HttpPost("rank-requests")]
        public async Task<IActionResult> SubmitRankUpgrade([FromBody] RankPromotionSubmitRequest request, CancellationToken ct)
        {
            var res = await _rankPromotion.SubmitAsync(AccountId, request, ct);
            return Ok(res);
        }

        [Authorize(Roles = "author,AUTHOR")]
        [HttpGet("rank-requests")]
        public async Task<IActionResult> MyRankRequests(CancellationToken ct)
        {
            var res = await _rankPromotion.ListMineAsync(AccountId, ct);
            return Ok(res);
        }
    }
}
