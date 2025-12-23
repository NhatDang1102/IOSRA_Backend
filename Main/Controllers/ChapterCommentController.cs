using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class ChapterCommentController : AppControllerBase
    {
        private readonly IChapterCommentService _chapterCommentService;

        public ChapterCommentController(IChapterCommentService chapterCommentService)
        {
            _chapterCommentService = chapterCommentService;
        }

        [HttpGet("chapter/{chapterId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByChapter(Guid chapterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var viewerId = TryGetAccountId();
            var result = await _chapterCommentService.GetByChapterAsync(chapterId, page, pageSize, ct, viewerId);
            return Ok(result);
        }

        [HttpGet("story/{storyId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByStory(Guid storyId, [FromQuery] Guid? chapterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var viewerId = TryGetAccountId();
            var result = await _chapterCommentService.GetByStoryAsync(storyId, chapterId, page, pageSize, ct, viewerId);
            return Ok(result);
        }

        // API Tạo bình luận mới (Có thể là bình luận gốc hoặc trả lời bình luận khác)
        // Flow: Client gửi nội dung -> Server check quyền -> Lưu comment -> Gửi thông báo cho tác giả/người được rep
        [HttpPost("{chapterId:guid}")]
        [Authorize]
        public async Task<IActionResult> Create(Guid chapterId, [FromBody] ChapterCommentCreateRequest request, CancellationToken ct = default)
        {
            var comment = await _chapterCommentService.CreateAsync(AccountId, chapterId, request, ct);
            return Ok(comment);
        }

        // API Chỉnh sửa bình luận
        // Chỉ cho phép sửa khi comment chưa bị khóa (locked) bởi admin/mod
        [HttpPut("{chapterId:guid}/{commentId:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid chapterId, Guid commentId, [FromBody] ChapterCommentUpdateRequest request, CancellationToken ct = default)
        {
            var comment = await _chapterCommentService.UpdateAsync(AccountId, chapterId, commentId, request, ct);
            return Ok(comment);
        }

        // API Xóa bình luận (Soft delete - chuyển status sang 'removed')
        [HttpDelete("{chapterId:guid}/{commentId:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid chapterId, Guid commentId, CancellationToken ct = default)
        {
            await _chapterCommentService.DeleteAsync(AccountId, chapterId, commentId, ct);
            return NoContent();
        }

        // API Thả cảm xúc (Like/Dislike) vào bình luận
        [HttpPost("{chapterId:guid}/{commentId:guid}/reaction")]
        [Authorize]
        public async Task<IActionResult> React(Guid chapterId, Guid commentId, [FromBody] ChapterCommentReactRequest request, CancellationToken ct = default)
        {
            var response = await _chapterCommentService.ReactAsync(AccountId, chapterId, commentId, request, ct);
            return Ok(response);
        }

        // API Hủy thả cảm xúc
        [HttpDelete("{chapterId:guid}/{commentId:guid}/reaction")]
        [Authorize]
        public async Task<IActionResult> RemoveReaction(Guid chapterId, Guid commentId, CancellationToken ct = default)
        {
            var response = await _chapterCommentService.RemoveReactionAsync(AccountId, chapterId, commentId, ct);
            return Ok(response);
        }

        [HttpGet("{chapterId:guid}/{commentId:guid}/reactions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetReactions(Guid chapterId, Guid commentId, [FromQuery] string reactionType, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _chapterCommentService.GetReactionsAsync(chapterId, commentId, reactionType, page, pageSize, ct);
            return Ok(result);
        }
    }
}
