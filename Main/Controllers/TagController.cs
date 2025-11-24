using Contract.DTOs.Request.Tag;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Tag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Implementations;
using Service.Interfaces;
using System;

namespace Main.Controllers;

[Route("api/[controller]")]
public class TagController : AppControllerBase
{
    private readonly ITagService _tag;

    public TagController(ITagService tag)
    {
        _tag = tag;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var data = await _tag.GetAllAsync(ct);
        return Ok(data);
    }

    [HttpGet("top")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TagOptionResponse>>> Top([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        return Ok(await _tag.GetTopOptionsAsync(limit, ct));
    }

    [HttpGet("options")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TagOptionResponse>>> Options(
    [FromQuery] string q,
    [FromQuery] int limit = 20,
    CancellationToken ct = default)
    {
        var data = await _tag.GetOptionsAsync(q, limit, ct);
        return Ok(data);
    }

    [HttpGet("paged")]
    [Authorize(Roles = "cmod,CONTENT_MOD,admin,ADMIN")]
    public async Task<ActionResult<PagedResult<TagPagedItem>>> GetPaged(
    [FromQuery] string? q,
    [FromQuery] bool asc = true,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
    {
        return Ok(await _tag.GetPagedAsync(q, "name", asc, page, pageSize, ct));
    }

    [HttpPost]
    [Authorize(Roles = "cmod,CONTENT_MOD,admin,ADMIN")]
    public async Task<IActionResult> Create([FromBody] TagCreateRequest req, CancellationToken ct)
    {
        var result = await _tag.CreateAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("resolve")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TagOptionResponse>>> Resolve([FromBody] TagResolveRequest body, CancellationToken ct = default)
    {
        return Ok(await _tag.ResolveOptionsAsync(body, ct));
    }

    [HttpPut("{tagId:guid}")]
    [Authorize(Roles = "cmod,CONTENT_MOD,admin,ADMIN")]
    public async Task<IActionResult> Update([FromRoute] Guid tagId, [FromBody] TagUpdateRequest req, CancellationToken ct)
    {
        var result = await _tag.UpdateAsync(tagId, req, ct);
        return Ok(result);
    }


}
