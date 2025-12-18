using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Repository.DataModels;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace Service.Services
{
    public class ChapterPurchaseService : IChapterPurchaseService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IChapterPurchaseRepository _chapterPurchaseRepository;
        private readonly IBillingRepository _billingRepository;
        private readonly INotificationService _notificationService;
        private readonly IProfileRepository _profileRepository;
        private readonly ILogger<ChapterPurchaseService> _logger;

        public ChapterPurchaseService(
            IChapterPurchaseRepository chapterPurchaseRepository,
            IBillingRepository billingRepository,
            INotificationService notificationService,
            IProfileRepository profileRepository,
            ILogger<ChapterPurchaseService> logger)
        {
            _chapterPurchaseRepository = chapterPurchaseRepository;
            _billingRepository = billingRepository;
            _notificationService = notificationService;
            _profileRepository = profileRepository;
            _logger = logger;
        }

        public async Task<ChapterPurchaseResponse> PurchaseAsync(Guid readerAccountId, Guid chapterId, CancellationToken ct = default)
        {
            await using var tx = await _chapterPurchaseRepository.BeginTransactionAsync(ct);

            var chapter = await _chapterPurchaseRepository.GetChapterForPurchaseAsync(chapterId, ct)
                ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotAvailable", "Chương không có sẵn để mua.", 400);
            }

            var story = chapter.story
                       ?? throw new AppException("StoryNotFound", "Thông tin truyện bị thiếu.", 404);

            var storyIsPublic = string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(story.status, "completed", StringComparison.OrdinalIgnoreCase);
            if (!storyIsPublic)
            {
                throw new AppException("StoryNotPublished", "Truyện phải được xuất bản trước khi có thể mua chương.", 400);
            }

            if (story.author_id == readerAccountId)
            {
                throw new AppException("AuthorCannotPurchase", "Tác giả đã sở hữu chương của họ.", 400);
            }

            if (!string.Equals(chapter.access_type, "dias", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterFree", "Chương này không yêu cầu mua.", 400);
            }

            var alreadyPurchased = await _chapterPurchaseRepository.HasReaderPurchasedChapterAsync(chapterId, readerAccountId, ct);
            if (alreadyPurchased)
            {
                throw new AppException("ChapterPurchased", "Bạn đã sở hữu chương này.", 409);
            }

            var wallet = await _billingRepository.GetOrCreateDiaWalletAsync(readerAccountId, ct);

            var priceDias = (long)chapter.dias_price;
            if (priceDias <= 0)
            {
                throw new AppException("InvalidPrice", "Giá chương chưa được định cấu hình.", 400);
            }

            if (wallet.balance_dias < priceDias)
            {
                throw new AppException("InsufficientBalance", "Không đủ dias trong ví.", 400);
            }

            var now = TimezoneConverter.VietnamNow;
            wallet.balance_dias -= priceDias;
            wallet.updated_at = now;

            var purchaseId = Guid.NewGuid();
            await _chapterPurchaseRepository.AddPurchaseLogAsync(new chapter_purchase_log
            {
                chapter_purchase_id = purchaseId,
                chapter_id = chapter.chapter_id,
                account_id = readerAccountId,
                dia_price = (uint)priceDias,
                created_at = now
            }, ct);

            await _billingRepository.AddWalletPaymentAsync(new wallet_payment
            {
                trs_id = Guid.NewGuid(),
                wallet_id = wallet.wallet_id,
                type = "purchase",
                dias_delta = -priceDias,
                dias_after = wallet.balance_dias,
                ref_id = purchaseId,
                created_at = now
            }, ct);

            var author = story.author
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả bị thiếu.", 404);

            //đổi format: 1 Dia mua = 1 Dia revenue (đơn vị Dias).
            //author ăn hết số Dias bán được. Việc chia sẻ doanh thu (RewardRate) sẽ tính khi Rút tiền.
            var authorShare = priceDias;
            author.revenue_balance += authorShare;

            var metadata = JsonSerializer.Serialize(new
            {
                chapterId = chapter.chapter_id,
                priceDias,
                buyerId = readerAccountId
            }, JsonOptions);

            await _chapterPurchaseRepository.AddAuthorRevenueTransactionAsync(new author_revenue_transaction
            {
                trans_id = Guid.NewGuid(),
                author_id = author.account_id,
                type = "purchase",
                amount = authorShare,
                purchase_log_id = purchaseId,
                metadata = metadata,
                created_at = now
            }, ct);

            await _chapterPurchaseRepository.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await NotifyChapterPurchaseAsync(story, chapter, readerAccountId, priceDias, ct);

            return new ChapterPurchaseResponse
            {
                PurchaseId = purchaseId,
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                ChapterTitle = chapter.title,
                PriceDias = (int)priceDias,
                WalletBalanceAfter = wallet.balance_dias,
                AuthorShareAmount = authorShare,
                PurchasedAt = now
            };
        }

        public async Task<ChapterVoicePurchaseResponse> PurchaseVoicesAsync(Guid readerAccountId, Guid chapterId, ChapterVoicePurchaseRequest request, CancellationToken ct = default)
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
                throw new AppException("VoiceSelectionRequired", "Lựa chọn giọng đọc không hợp lệ.", 400);
            }

            await using var tx = await _chapterPurchaseRepository.BeginTransactionAsync(ct);

            var chapter = await _chapterPurchaseRepository.GetChapterWithVoicesAsync(chapterId, ct)
                ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotAvailable", "Chương không có sẵn.", 400);
            }

            var story = chapter.story
                       ?? throw new AppException("StoryNotFound", "Thông tin truyện bị thiếu.", 404);

            var storyIsPublic = string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(story.status, "completed", StringComparison.OrdinalIgnoreCase);
            if (!storyIsPublic)
            {
                throw new AppException("StoryNotPublished", "Truyện phải được xuất bản.", 400);
            }

            if (story.author_id == readerAccountId)
            {
                throw new AppException("AuthorCannotPurchase", "Tác giả đã sở hữu giọng đọc chương của họ.", 400);
            }

            if (string.Equals(chapter.access_type, "dias", StringComparison.OrdinalIgnoreCase))
            {
                var hasChapter = await _chapterPurchaseRepository.HasReaderPurchasedChapterAsync(chapterId, readerAccountId, ct);
                if (!hasChapter)
                {
                    throw new AppException("ChapterNotPurchased", "Bạn phải mua chương này trước khi mua giọng đọc.", 403);
                }
            }

            var readyVoices = chapter.chapter_voices
                .Where(v => requestedVoiceIds.Contains(v.voice_id) &&
                            string.Equals(v.status, "ready", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (readyVoices.Count != requestedVoiceIds.Count)
            {
                throw new AppException("VoiceUnavailable", "Một hoặc nhiều giọng đọc được yêu cầu không khả dụng.", 400);
            }

            var purchasedVoiceIds = await _chapterPurchaseRepository.GetPurchasedVoiceIdsAsync(chapterId, readerAccountId, ct);
            var newVoices = readyVoices
                .Where(v => !purchasedVoiceIds.Contains(v.voice_id))
                .ToList();

            if (newVoices.Count == 0)
            {
                throw new AppException("VoiceAlreadyOwned", "Bạn đã mua các giọng đọc được chọn.", 409);
            }

            var totalDias = newVoices.Sum(v => (long)v.dias_price);
            if (totalDias <= 0)
            {
                throw new AppException("InvalidVoicePrice", "Giá giọng đọc chưa được định cấu hình.", 500);
            }

            var wallet = await _billingRepository.GetOrCreateDiaWalletAsync(readerAccountId, ct);
            if (wallet.balance_dias < totalDias)
            {
                throw new AppException("InsufficientBalance", "Không đủ dias trong ví.", 400);
            }

            var now = TimezoneConverter.VietnamNow;
            wallet.balance_dias -= totalDias;
            wallet.updated_at = now;

            var purchaseId = Guid.NewGuid();
            var purchaseLog = new voice_purchase_log
            {
                voice_purchase_id = purchaseId,
                account_id = readerAccountId,
                chapter_id = chapter.chapter_id,
                total_dias = (uint)totalDias,
                created_at = now
            };
            await _chapterPurchaseRepository.AddVoicePurchaseLogAsync(purchaseLog, ct);

            var purchaseVoices = new List<ChapterPurchasedVoiceResponse>(newVoices.Count);
            foreach (var voice in newVoices)
            {
                var entry = new voice_purchase_item
                {
                    purchase_item_id = Guid.NewGuid(),
                    voice_purchase_id = purchaseId,
                    account_id = readerAccountId,
                    chapter_id = chapter.chapter_id,
                    voice_id = voice.voice_id,
                    dia_price = voice.dias_price,
                    created_at = now
                };
                await _chapterPurchaseRepository.AddVoicePurchaseItemAsync(entry, ct);

                purchaseVoices.Add(new ChapterPurchasedVoiceResponse
                {
                    VoiceId = voice.voice_id,
                    VoiceName = voice.voice?.voice_name ?? string.Empty,
                    VoiceCode = voice.voice?.voice_code ?? string.Empty,
                    PriceDias = (int)voice.dias_price
                });
            }

            await _billingRepository.AddWalletPaymentAsync(new wallet_payment
            {
                trs_id = Guid.NewGuid(),
                wallet_id = wallet.wallet_id,
                type = "purchase",
                dias_delta = -totalDias,
                dias_after = wallet.balance_dias,
                ref_id = purchaseId,
                created_at = now
            }, ct);

            var author = story.author
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả bị thiếu.", 404);

            // Logic mới: 1 Dia mua = 1 Dia revenue.
            var authorShare = totalDias;
            author.revenue_balance += authorShare;

            var metadata = JsonSerializer.Serialize(new
            {
                chapterId = chapter.chapter_id,
                voiceIds = newVoices.Select(v => v.voice_id),
                priceDias = totalDias,
                buyerId = readerAccountId
            }, JsonOptions);

            await _chapterPurchaseRepository.AddAuthorRevenueTransactionAsync(new author_revenue_transaction
            {
                trans_id = Guid.NewGuid(),
                author_id = author.account_id,
                type = "purchase",
                amount = authorShare,
                voice_purchase_id = purchaseId,
                metadata = metadata,
                created_at = now
            }, ct);

            await _chapterPurchaseRepository.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await NotifyVoicePurchaseAsync(story, chapter, readerAccountId, newVoices, totalDias, ct);

            return new ChapterVoicePurchaseResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                TotalPriceDias = (int)totalDias,
                WalletBalanceAfter = wallet.balance_dias,
                AuthorShareAmount = authorShare,
                PurchasedAt = now,
                Voices = purchaseVoices.ToArray()
            };
        }

        public async Task<IReadOnlyList<PurchasedChapterResponse>> GetPurchasedChaptersAsync(Guid readerAccountId, Guid? storyId, CancellationToken ct = default)
        {
            var data = await _chapterPurchaseRepository.GetPurchasedChaptersAsync(readerAccountId, storyId, ct);
            if (data.Count == 0)
            {
                return Array.Empty<PurchasedChapterResponse>();
            }

            var chapterIds = data.Select(d => d.ChapterId).Distinct().ToList();
            var voiceData = await _chapterPurchaseRepository.GetPurchasedVoicesAsync(readerAccountId, chapterIds, ct);
            var voicesByChapter = voiceData
                .GroupBy(v => v.ChapterId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(MapPurchasedVoiceResponse).ToArray());

            return data.Select(d => new PurchasedChapterResponse
            {
                PurchaseId = d.ChapterPurchaseId,
                ChapterId = d.ChapterId,
                StoryId = d.StoryId,
                StoryTitle = d.StoryTitle,
                ChapterNo = d.ChapterNo,
                ChapterTitle = d.ChapterTitle,
                PriceDias = (int)d.PriceDias,
                PurchasedAt = d.PurchasedAt,
                Voices = voicesByChapter.TryGetValue(d.ChapterId, out var voices)
                    ? voices
                    : Array.Empty<PurchasedVoiceResponse>()
            }).ToArray();
        }

        public async Task<IReadOnlyList<PurchasedVoiceResponse>> GetPurchasedVoicesAsync(Guid readerAccountId, Guid chapterId, CancellationToken ct = default)
        {
            var data = await _chapterPurchaseRepository.GetPurchasedVoicesAsync(readerAccountId, chapterId, ct);
            return data.Select(MapPurchasedVoiceResponse).ToArray();
        }

        public async Task<IReadOnlyList<PurchasedVoiceHistoryResponse>> GetPurchasedVoiceHistoryAsync(Guid readerAccountId, CancellationToken ct = default)
        {
            var data = await _chapterPurchaseRepository.GetPurchasedVoicesAsync(readerAccountId, ct);
            if (data.Count == 0)
            {
                return Array.Empty<PurchasedVoiceHistoryResponse>();
            }

            var storyGroups = data
                .GroupBy(d => new { d.StoryId, d.StoryTitle })
                .OrderBy(g => g.Key.StoryTitle)
                .ThenBy(g => g.Min(v => v.PurchasedAt));

            return storyGroups.Select(storyGroup => new PurchasedVoiceHistoryResponse
            {
                StoryId = storyGroup.Key.StoryId,
                StoryTitle = storyGroup.Key.StoryTitle,
                Chapters = storyGroup
                    .GroupBy(v => new { v.ChapterId, v.ChapterNo, v.ChapterTitle })
                    .OrderBy(g => g.Key.ChapterNo)
                    .ThenBy(g => g.Min(v => v.PurchasedAt))
                    .Select(chapterGroup => new PurchasedVoiceHistoryChapter
                    {
                        ChapterId = chapterGroup.Key.ChapterId,
                        ChapterNo = chapterGroup.Key.ChapterNo,
                        ChapterTitle = chapterGroup.Key.ChapterTitle,
                        Voices = chapterGroup
                            .OrderBy(v => v.VoiceName)
                            .ThenBy(v => v.PurchasedAt)
                            .Select(MapPurchasedVoiceResponse)
                            .ToArray()
                    })
                    .ToArray()
            }).ToArray();
        }

        private static PurchasedVoiceResponse MapPurchasedVoiceResponse(PurchasedVoiceData data)
            => new PurchasedVoiceResponse
            {
                PurchaseVoiceId = data.PurchaseVoiceId,
                ChapterId = data.ChapterId,
                StoryId = data.StoryId,
                VoiceId = data.VoiceId,
                VoiceName = data.VoiceName,
                VoiceCode = data.VoiceCode,
                PriceDias = (int)data.PriceDias,
                AudioUrl = data.AudioUrl,
                PurchasedAt = data.PurchasedAt
            };

        private async Task NotifyChapterPurchaseAsync(story story, chapter chapter, Guid buyerAccountId, long priceDias, CancellationToken ct)
        {
            try
            {
                var authorAccount = story.author?.account;
                if (authorAccount == null)
                {
                    return;
                }

                var buyerAccount = await _profileRepository.GetAccountByIdAsync(buyerAccountId, ct);
                if (buyerAccount == null)
                {
                    return;
                }

                var buyerName = buyerAccount.username;
                var title = $"{buyerName} đã mua chương của bạn";
                var message = $"{buyerName} vừa mua Chương {(int)chapter.chapter_no} - \"{chapter.title}\" với {priceDias} dias.";

                await _notificationService.CreateAsync(new NotificationCreateModel(
                    authorAccount.account_id,
                    NotificationTypes.ChapterPurchase,
                    title,
                    message,
                    new
                    {
                        storyId = story.story_id,
                        chapterId = chapter.chapter_id,
                        chapterNo = (int)chapter.chapter_no,
                        priceDias,
                        buyerId = buyerAccount.account_id,
                        buyerUsername = buyerName
                    }), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chapter purchase notification for chapter {ChapterId}", chapter.chapter_id);
            }
        }

        private async Task NotifyVoicePurchaseAsync(story story, chapter chapter, Guid buyerAccountId, IReadOnlyList<chapter_voice> voices, long totalDias, CancellationToken ct)
        {
            if (voices == null || voices.Count == 0)
            {
                return;
            }

            try
            {
                var authorAccount = story.author?.account;
                if (authorAccount == null)
                {
                    return;
                }

                var buyerAccount = await _profileRepository.GetAccountByIdAsync(buyerAccountId, ct);
                if (buyerAccount == null)
                {
                    return;
                }

                var buyerName = buyerAccount.username;
                var voiceNames = voices
                    .Select(v => v.voice?.voice_name ?? "Voice")
                    .ToArray();

                var title = $"{buyerName} đã mua voice chương của bạn";
                var message = $"{buyerName} vừa mua {voices.Count} voice cho Chương {(int)chapter.chapter_no} - \"{chapter.title}\": {string.Join(", ", voiceNames)}.";

                await _notificationService.CreateAsync(new NotificationCreateModel(
                    authorAccount.account_id,
                    NotificationTypes.VoicePurchase,
                    title,
                    message,
                    new
                    {
                        storyId = story.story_id,
                        chapterId = chapter.chapter_id,
                        chapterNo = (int)chapter.chapter_no,
                        totalDias,
                        voiceIds = voices.Select(v => v.voice_id),
                        buyerId = buyerAccount.account_id,
                        buyerUsername = buyerName
                    }), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send voice purchase notification for chapter {ChapterId}", chapter.chapter_id);
            }
        }

    }
}