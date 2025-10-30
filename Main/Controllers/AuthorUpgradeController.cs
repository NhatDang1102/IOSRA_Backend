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

        public AuthorUpgradeController(IAuthorUpgradeService authorUpgrade)
        {
            _authorUpgrade = authorUpgrade;
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
    }
}
