using System;
using Contract.DTOs.Request.Tag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

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

    [HttpPost]
    [Authorize(Roles = "cmod,CONTENT_MOD,admin,ADMIN")]
    public async Task<IActionResult> Create([FromBody] TagCreateRequest req, CancellationToken ct)
    {
        var result = await _tag.CreateAsync(req, ct);
        return Ok(result);
    }

    [HttpPut("{tagId:guid}")]
    [Authorize(Roles = "cmod,CONTENT_MOD,admin,ADMIN")]
    public async Task<IActionResult> Update([FromRoute] Guid tagId, [FromBody] TagUpdateRequest req, CancellationToken ct)
    {
        var result = await _tag.UpdateAsync(tagId, req, ct);
        return Ok(result);
    }

    [HttpDelete("{tagId:guid}")]
    [Authorize(Roles = "cmod,CONTENT_MOD,admin,ADMIN")]
    public async Task<IActionResult> Delete([FromRoute] Guid tagId, CancellationToken ct)
    {
        await _tag.DeleteAsync(tagId, ct);
        return NoContent();
    }
}
