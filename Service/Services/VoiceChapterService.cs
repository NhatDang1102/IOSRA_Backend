using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Voice;
using Contract.DTOs.Response.Voice;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using Service.Models;

namespace Service.Services
{
    public class VoiceChapterService : IVoiceChapterService
    {
        private readonly AppDbContext _db;
        private readonly IChapterContentStorage _contentStorage;
        private readonly IVoicePricingService _voicePricingService;
        private readonly IVoiceSynthesisQueue _voiceQueue;

        public VoiceChapterService(
            AppDbContext db,
            IChapterContentStorage contentStorage,
            IVoicePricingService voicePricingService,
            IVoiceSynthesisQueue voiceQueue)
        {
            _db = db;
            _contentStorage = contentStorage;
            _voicePricingService = voicePricingService;
            _voiceQueue = voiceQueue;
        }

        public async Task<VoiceChapterStatusResponse> GetAsync(Guid requesterAccountId, Guid chapterId, CancellationToken ct = default)
        {
            var chapter = await LoadAuthorChapterAsync(chapterId, requesterAccountId, includeVoices: true, ct);
            var charCount = (int)chapter.char_count;
            int generationCost = 0;
            if (charCount > 0)
            {
                try
                {
                    generationCost = await _voicePricingService.GetGenerationCostAsync(charCount, ct);
                }
                catch
                {
                    // Ignore pricing errors for status check
                }
            }
            return MapChapter(chapter, charCount, generationCost);
        }

        public async Task<VoiceChapterCharCountResponse> GetCharCountAsync(Guid authorAccountId, Guid chapterId, CancellationToken ct = default)
        {
            var chapter = await LoadAuthorChapterAsync(chapterId, authorAccountId, includeVoices: false, ct);
            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Nội dung chương bị thiếu trong kho lưu trữ.", 400);
            }

            var content = (await _contentStorage.DownloadAsync(chapter.content_url, ct)).Trim();
            if (content.Length == 0)
            {
                throw new AppException("ChapterContentEmpty", "Nội dung chương bị trống.", 400);
            }

            return new VoiceChapterCharCountResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterTitle = chapter.title,
                WordCount = (int)chapter.word_count,
                CharacterCount = content.Length
            };
        }

        public async Task<IReadOnlyList<VoicePresetResponse>> GetPresetsAsync(CancellationToken ct = default)
        {
            var presets = await _db.voice_lists
                .AsNoTracking()
                .Where(v => v.is_active)
                .OrderBy(v => v.voice_name)
                .ToListAsync(ct);

            return presets.Select(v => new VoicePresetResponse
            {
                VoiceId = v.voice_id,
                VoiceName = v.voice_name,
                VoiceCode = v.voice_code,
                ProviderVoiceId = v.provider_voice_id,
                Description = v.description ?? string.Empty
            }).ToArray();
        }

        public async Task<VoiceChapterOrderResponse> OrderVoicesAsync(Guid authorAccountId, Guid chapterId, VoiceChapterOrderRequest request, CancellationToken ct = default)
        {
            if (request?.VoiceIds == null || request.VoiceIds.Count == 0)
            {
                throw new AppException("VoiceSelectionRequired", "Vui lòng chọn ít nhất một giọng đọc.", 400);
            }

            var requestedVoiceIds = request.VoiceIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (requestedVoiceIds.Count == 0)
            {
                throw new AppException("VoiceSelectionRequired", "Các giọng đọc được chọn không hợp lệ.", 400);
            }

            var chapter = await LoadAuthorChapterAsync(chapterId, authorAccountId, includeVoices: true, ct);
            EnsureVoiceEligibleChapter(chapter);
            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPublished", "Chỉ các chương đã xuất bản mới có thể chuyển đổi thành âm thanh.", 400);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Nội dung chương bị thiếu trong kho lưu trữ.", 400);
            }

            var presets = await _db.voice_lists
                .Where(v => requestedVoiceIds.Contains(v.voice_id) && v.is_active)
                .ToListAsync(ct);

            if (presets.Count != requestedVoiceIds.Count)
            {
                throw new AppException("VoicePresetNotFound", "Một hoặc nhiều giọng đọc được yêu cầu không khả dụng.", 404);
            }

            var existingVoices = new HashSet<Guid>(chapter.chapter_voices.Select(v => v.voice_id));
            var newPresets = presets.Where(v => !existingVoices.Contains(v.voice_id)).ToList();
            if (newPresets.Count == 0)
            {
                throw new AppException("VoiceAlreadyGenerated", "Tất cả các giọng đọc được yêu cầu đã được tạo.", 400);
            }

            var content = (await _contentStorage.DownloadAsync(chapter.content_url, ct)).Trim();
            if (content.Length == 0)
            {
                throw new AppException("ChapterContentEmpty", "Nội dung chương bị trống.", 400);
            }

            var charPerVoice = content.Length;
            chapter.char_count = charPerVoice;
            
            // Lấy chi phí tạo voice bằng Dias
            var generationCostDias = await _voicePricingService.GetGenerationCostAsync(charPerVoice, ct);
            var totalGenerationCost = generationCostDias * newPresets.Count;

            // Lấy author entity để trừ tiền
            var author = await _db.authors.Include(a => a.account).FirstOrDefaultAsync(a => a.account_id == authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Không tìm thấy hồ sơ tác giả.", 404);

            // Kiểm tra và trừ từ revenue_balance
            if (author.revenue_balance < totalGenerationCost)
            {
                throw new AppException("InsufficientRevenue", "Số dư doanh thu không đủ để tạo giọng đọc. Vui lòng kiếm thêm Dias.", 400, new
                {
                    required = totalGenerationCost,
                    available = author.revenue_balance
                });
            }

            author.revenue_balance -= totalGenerationCost;
            
            var now = TimezoneConverter.VietnamNow;
            // var voicePrice = await _voicePricingService.GetPriceAsync(charPerVoice, ct); // Sử dụng GetGenerationCost thay thế

            var chapterVoiceRows = new List<chapter_voice>();
            foreach (var preset in newPresets)
            {
                var entity = new chapter_voice
                {
                    chapter_id = chapter.chapter_id,
                    voice_id = preset.voice_id,
                    voice = preset,
                    status = "pending",
                    requested_at = now,
                    char_cost = charPerVoice,
                    dias_price = (uint)await _voicePricingService.GetPriceAsync(charPerVoice, ct) // Giá bán cho Reader
                };
                chapter.chapter_voices.Add(entity);
                chapterVoiceRows.Add(entity);
            }
            var voiceJobIds = chapterVoiceRows.Select(v => v.voice_id).ToArray();

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            
            // Ghi log vào author_revenue_transaction
            await _db.author_revenue_transaction.AddAsync(new author_revenue_transaction
            {
                trans_id = Guid.NewGuid(),
                author_id = authorAccountId,
                type = "voice_generation",
                amount = -totalGenerationCost,
                metadata = JsonSerializer.Serialize(new
                {
                    chapterId = chapter.chapter_id,
                    charCount = charPerVoice,
                    generatedVoices = newPresets.Select(p => p.voice_id).ToArray(),
                    costDias = totalGenerationCost
                }),
                created_at = now
            }, ct);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            foreach (var voiceId in voiceJobIds)
            {
                await _voiceQueue.EnqueueAsync(new VoiceSynthesisJob(chapter.chapter_id, voiceId), ct);
            }

            return new VoiceChapterOrderResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterTitle = chapter.title,
                Voices = chapter.chapter_voices
                    .OrderBy(v => v.voice?.voice_name)
                    .Select(MapVoice)
                    .ToArray(),
                TotalGenerationCostDias = totalGenerationCost,
                AuthorRevenueBalanceAfter = author.revenue_balance
            };
        }

        private void EnsureVoiceEligibleChapter(chapter chapter)
        {
            var authorRank = chapter.story?.author?.rank?.rank_name;
            if (string.IsNullOrWhiteSpace(authorRank) || string.Equals(authorRank, "Casual", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("VoiceNotAllowed", "Rank đồng trở lên mới được tạo voice.", 403);
            }
        }

        private async Task<chapter> LoadAuthorChapterAsync(Guid chapterId, Guid authorAccountId, bool includeVoices, CancellationToken ct)
        {
            IQueryable<chapter> query = _db.chapter
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.rank);

            if (includeVoices)
            {
                query = query.Include(c => c.chapter_voices)
                    .ThenInclude(v => v.voice);
            }

            var chapter = await query.FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            if (chapter.story.author_id != authorAccountId)
            {
                throw new AppException("ChapterAccessDenied", "Bạn không được phép sửa đổi chương này.", 403);
            }

            return chapter;
        }

        private VoiceChapterStatusResponse MapChapter(chapter chapter, int charCount, int generationCost)
        {
            var voices = chapter.chapter_voices
                .OrderBy(v => v.voice?.voice_name)
                .Select(MapVoice)
                .ToArray();

            return new VoiceChapterStatusResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterTitle = chapter.title,
                CharCount = charCount,
                GenerationCostPerVoiceDias = generationCost,
                Voices = voices
            };
        }

        private VoiceChapterVoiceResponse MapVoice(chapter_voice entity)
        {
            var audioPath = entity.storage_path;

            return new VoiceChapterVoiceResponse
            {
                VoiceId = entity.voice_id,
                VoiceName = entity.voice?.voice_name ?? string.Empty,
                VoiceCode = entity.voice?.voice_code ?? string.Empty,
                Status = entity.status,
                AudioUrl = audioPath,
                RequestedAt = entity.requested_at,
                CompletedAt = entity.completed_at,
                CharCost = entity.char_cost,
                PriceDias = (int)entity.dias_price,
                ErrorMessage = entity.error_message
            };
        }

    }
}