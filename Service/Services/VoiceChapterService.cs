using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Voice;
using Contract.DTOs.Response.Voice;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Repository.DBContext;
using Repository.Entities;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class VoiceChapterService : IVoiceChapterService
    {
        private readonly AppDbContext _db;
        private readonly IChapterContentStorage _contentStorage;
        private readonly IVoiceAudioStorage _voiceStorage;
        private readonly IElevenLabsClient _elevenLabsClient;
        private readonly ILogger<VoiceChapterService> _logger;
        private readonly IVoicePricingService _voicePricingService;

        public VoiceChapterService(
            AppDbContext db,
            IChapterContentStorage contentStorage,
            IVoiceAudioStorage voiceStorage,
            IElevenLabsClient elevenLabsClient,
            IVoicePricingService voicePricingService,
            ILogger<VoiceChapterService> logger)
        {
            _db = db;
            _contentStorage = contentStorage;
            _voiceStorage = voiceStorage;
            _elevenLabsClient = elevenLabsClient;
            _voicePricingService = voicePricingService;
            _logger = logger;
        }

        public async Task<VoiceChapterStatusResponse> GetAsync(Guid requesterAccountId, Guid chapterId, CancellationToken ct = default)
        {
            var chapter = await LoadAuthorChapterAsync(chapterId, requesterAccountId, includeVoices: true, ct);
            return MapChapter(chapter);
        }

        public async Task<VoiceChapterCharCountResponse> GetCharCountAsync(Guid authorAccountId, Guid chapterId, CancellationToken ct = default)
        {
            var chapter = await LoadAuthorChapterAsync(chapterId, authorAccountId, includeVoices: false, ct);
            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Chapter content is missing from storage.", 400);
            }

            var content = (await _contentStorage.DownloadAsync(chapter.content_url, ct)).Trim();
            if (content.Length == 0)
            {
                throw new AppException("ChapterContentEmpty", "Chapter content is empty.", 400);
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
                throw new AppException("VoiceSelectionRequired", "Please select at least one voice preset.", 400);
            }

            var requestedVoiceIds = request.VoiceIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (requestedVoiceIds.Count == 0)
            {
                throw new AppException("VoiceSelectionRequired", "Invalid voice presets selected.", 400);
            }

            var chapter = await LoadAuthorChapterAsync(chapterId, authorAccountId, includeVoices: true, ct);
            EnsureVoiceEligibleChapter(chapter);
            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPublished", "Only published chapters can be converted to audio.", 400);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Chapter content is missing from storage.", 400);
            }

            var presets = await _db.voice_lists
                .Where(v => requestedVoiceIds.Contains(v.voice_id) && v.is_active)
                .ToListAsync(ct);

            if (presets.Count != requestedVoiceIds.Count)
            {
                throw new AppException("VoicePresetNotFound", "One or more requested voices are not available.", 404);
            }

            var existingVoices = new HashSet<Guid>(chapter.chapter_voices.Select(v => v.voice_id));
            var newPresets = presets.Where(v => !existingVoices.Contains(v.voice_id)).ToList();
            if (newPresets.Count == 0)
            {
                throw new AppException("VoiceAlreadyGenerated", "All requested voices have already been generated.", 400);
            }

            var wallet = await _db.voice_wallets.FirstOrDefaultAsync(w => w.account_id == authorAccountId, ct);
            if (wallet == null)
            {
                throw new AppException("VoiceWalletMissing", "Please top-up voice characters before ordering.", 400);
            }

            var content = (await _contentStorage.DownloadAsync(chapter.content_url, ct)).Trim();
            if (content.Length == 0)
            {
                throw new AppException("ChapterContentEmpty", "Chapter content is empty.", 400);
            }

            var charPerVoice = content.Length;
            chapter.char_count = charPerVoice;
            var totalCharsNeeded = (long)charPerVoice * newPresets.Count;
            if (wallet.balance_chars < totalCharsNeeded)
            {
                throw new AppException("VoiceBalanceInsufficient", "Not enough voice characters. Please top-up.", 400, new
                {
                    required = totalCharsNeeded,
                    available = wallet.balance_chars
                });
            }

            var now = TimezoneConverter.VietnamNow;
            var voicePrice = await _voicePricingService.GetPriceAsync(charPerVoice, ct);

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
                    dias_price = (uint)voicePrice
                };
                chapter.chapter_voices.Add(entity);
                chapterVoiceRows.Add(entity);
            }

            wallet.balance_chars -= totalCharsNeeded;
            wallet.updated_at = now;

            var paymentLog = new voice_wallet_payment
            {
                trs_id = Guid.NewGuid(),
                wallet_id = wallet.wallet_id,
                type = "purchase",
                char_delta = -totalCharsNeeded,
                char_after = wallet.balance_chars,
                ref_id = chapter.chapter_id,
                created_at = now,
                note = $"Voice order ({newPresets.Count} preset(s))"
            };

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            _db.voice_wallet_payments.Add(paymentLog);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            foreach (var preset in newPresets)
            {
                var row = chapterVoiceRows.First(r => r.voice_id == preset.voice_id);
                await GenerateVoiceAsync(chapter, row, preset, content, ct);
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
                CharactersCharged = totalCharsNeeded,
                WalletBalance = wallet.balance_chars
            };
        }

        private void EnsureVoiceEligibleChapter(chapter chapter)
        {
            var authorRank = chapter.story?.author?.rank?.rank_name;
            if (string.IsNullOrWhiteSpace(authorRank) || string.Equals(authorRank, "Casual", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("VoiceNotAllowed", "Only Bronze rank or higher authors can generate voice.", 403);
            }
        }

        private async Task<chapter> LoadAuthorChapterAsync(Guid chapterId, Guid authorAccountId, bool includeVoices, CancellationToken ct)
        {
            IQueryable<chapter> query = _db.chapters
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.rank);

            if (includeVoices)
            {
                query = query.Include(c => c.chapter_voices)
                    .ThenInclude(v => v.voice);
            }

            var chapter = await query.FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Chapter not found.", 404);

            if (chapter.story.author_id != authorAccountId)
            {
                throw new AppException("ChapterAccessDenied", "You are not allowed to modify this chapter.", 403);
            }

            return chapter;
        }

        private VoiceChapterStatusResponse MapChapter(chapter chapter)
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

        private async Task GenerateVoiceAsync(chapter chapter, chapter_voice row, voice_list preset, string content, CancellationToken ct)
        {
            try
            {
                row.voice ??= preset;
                row.status = "processing";
                row.error_message = null;
                row.completed_at = null;
                await _db.SaveChangesAsync(ct);

                var audioBytes = await _elevenLabsClient.SynthesizeAsync(preset.provider_voice_id, content, ct);
                var storageKey = await _voiceStorage.UploadAsync(chapter.story_id, chapter.chapter_id, preset.voice_id, audioBytes, ct);

                row.storage_path = storageKey;
                row.status = "ready";
                row.completed_at = TimezoneConverter.VietnamNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voice synthesis failed for chapter {ChapterId} voice preset {VoiceId}", chapter.chapter_id, preset.voice_id);
                row.status = "failed";
                row.error_message = ex.Message;
                row.completed_at = TimezoneConverter.VietnamNow;
            }
            finally
            {
                await _db.SaveChangesAsync(ct);
            }
        }
    }
}
