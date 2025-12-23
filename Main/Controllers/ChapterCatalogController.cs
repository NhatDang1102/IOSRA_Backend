using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Exceptions;
using Service.Interfaces;

namespace Main.Controllers
{
    // Controller hiển thị nội dung chương truyện và danh sách chương (Mục lục)
    [Route("api/ChapterCatalog")]
    [AllowAnonymous]
    public class ChapterCatalogController : AppControllerBase
    {
        private readonly IChapterCatalogService _chapterCatalogService;
        private readonly IStoryViewTracker _storyViewTracker;

        public ChapterCatalogController(IChapterCatalogService chapterCatalogService, IStoryViewTracker storyViewTracker)
        {
            _chapterCatalogService = chapterCatalogService;
            _storyViewTracker = storyViewTracker;
        }

        // API Lấy danh sách chương (Mục lục) của một truyện
        // Có hỗ trợ phân trang
        [HttpGet]
        public async Task<ActionResult<PagedResult<ChapterCatalogListItemResponse>>> List([FromQuery] ChapterCatalogQuery query, CancellationToken ct)
        {
            if (query.StoryId == Guid.Empty)
            {
                throw new AppException("ValidationFailed", "storyId là bắt buộc.", 400);
            }

            // Lấy ID người dùng hiện tại (nếu đã đăng nhập) để check xem họ đã mua chương nào chưa
            query.ViewerAccountId = TryGetAccountId();
            var chapters = await _chapterCatalogService.GetChaptersAsync(query, ct);
            return Ok(chapters);
        }

        // API Lấy nội dung chi tiết của một chương (Để đọc)
        // Flow:
        // 1. Kiểm tra quyền truy cập (Free/Premium/Đã mua).
        // 2. Trả về nội dung text + link audio (nếu có).
        // 3. Ghi nhận lượt xem (View Count) -> Quan trọng cho việc tính tiền tác giả và xếp hạng truyện.
        [HttpGet("{chapterId:guid}")]
        public async Task<ActionResult<ChapterCatalogDetailResponse>> Get(Guid chapterId, CancellationToken ct)
        {
            var viewerAccountId = TryGetAccountId();
            
            // Service sẽ throw Exception nếu user không có quyền xem (chưa mua chương khóa)
            var chapter = await _chapterCatalogService.GetChapterAsync(chapterId, ct, viewerAccountId);

            // Ghi nhận lượt xem (View Tracking)
            // Sử dụng IP (fingerprint) để hạn chế spam view từ một máy (cơ bản)
            var fingerprint = HttpContext.Connection.RemoteIpAddress?.ToString();
            
            // Fire & Forget (có await nhưng là async task background trong tracker) hoặc await nhanh
            await _storyViewTracker.RecordViewAsync(chapter.StoryId, viewerAccountId, fingerprint, ct);

            return Ok(chapter);
        }

        // API Lấy danh sách các giọng đọc (AI Voice) khả dụng cho chương này
        [HttpGet("{chapterId:guid}/voices")]
        public async Task<ActionResult<IReadOnlyList<ChapterCatalogVoiceResponse>>> GetVoices(Guid chapterId, CancellationToken ct)
        {
            var result = await _chapterCatalogService.GetChapterVoicesAsync(chapterId, TryGetAccountId(), ct);
            return Ok(result);
        }

        // API Lấy chi tiết một giọng đọc cụ thể (để nghe thử hoặc kiểm tra giá)
        [HttpGet("{chapterId:guid}/voices/{voiceId:guid}")]
        public async Task<ActionResult<ChapterCatalogVoiceResponse>> GetVoice(Guid chapterId, Guid voiceId, CancellationToken ct)
        {
            var result = await _chapterCatalogService.GetChapterVoiceAsync(chapterId, voiceId, TryGetAccountId(), ct);
            return Ok(result);
        }
    }
}