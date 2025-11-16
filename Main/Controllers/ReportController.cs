using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class ReportController : AppControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] ReportCreateRequest request, CancellationToken ct = default)
        {
            var result = await _reportService.CreateAsync(AccountId, request, ct);
            return Ok(result);
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyReports([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _reportService.GetMyReportsAsync(AccountId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{reportId:guid}")]
        [Authorize]
        public async Task<IActionResult> GetMyReport(Guid reportId, CancellationToken ct = default)
        {
            var result = await _reportService.GetMyReportAsync(AccountId, reportId, ct);
            return Ok(result);
        }

    }
}
