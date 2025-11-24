using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Chapter;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ChapterPurchaseService : IChapterPurchaseService
    {
        private const int VndPerDia = 100;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IChapterPurchaseRepository _chapterPurchaseRepository;
        private readonly IBillingRepository _billingRepository;

        public ChapterPurchaseService(
            IChapterPurchaseRepository chapterPurchaseRepository,
            IBillingRepository billingRepository)
        {
            _chapterPurchaseRepository = chapterPurchaseRepository;
            _billingRepository = billingRepository;
        }

        public async Task<ChapterPurchaseResponse> PurchaseAsync(Guid readerAccountId, Guid chapterId, CancellationToken ct = default)
        {
            await using var tx = await _chapterPurchaseRepository.BeginTransactionAsync(ct);

            var chapter = await _chapterPurchaseRepository.GetChapterForPurchaseAsync(chapterId, ct)
                ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotAvailable", "Chapter is not available for purchase.", 400);
            }

            var story = chapter.story
                       ?? throw new AppException("StoryNotFound", "Story information is missing.", 404);

            if (!string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryNotPublished", "Story must be published before chapters can be purchased.", 400);
            }

            if (story.author_id == readerAccountId)
            {
                throw new AppException("AuthorCannotPurchase", "Authors already own their chapters.", 400);
            }

            if (!string.Equals(chapter.access_type, "dias", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterFree", "This chapter does not require a purchase.", 400);
            }

            var alreadyPurchased = await _chapterPurchaseRepository.HasReaderPurchasedChapterAsync(chapterId, readerAccountId, ct);
            if (alreadyPurchased)
            {
                throw new AppException("ChapterPurchased", "You already own this chapter.", 409);
            }

            var wallet = await _billingRepository.GetOrCreateDiaWalletAsync(readerAccountId, ct);

            var priceDias = (long)chapter.dias_price;
            if (priceDias <= 0)
            {
                throw new AppException("InvalidPrice", "Chapter price is not configured.", 400);
            }

            if (wallet.balance_coin < priceDias)
            {
                throw new AppException("InsufficientBalance", "Not enough dias in wallet.", 400);
            }

            var now = TimezoneConverter.VietnamNow;
            wallet.balance_coin -= priceDias;
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
                coin_delta = -priceDias,
                coin_after = wallet.balance_coin,
                ref_id = purchaseId,
                created_at = now
            }, ct);

            var author = story.author
                         ?? throw new AppException("AuthorNotFound", "Author profile is missing.", 404);

            var grossVnd = priceDias * VndPerDia;
            var rewardRate = author.rank?.reward_rate ?? 0m;
            var authorShare = (long)Math.Round(grossVnd * (rewardRate / 100m), MidpointRounding.AwayFromZero);
            if (authorShare < 0)
            {
                authorShare = 0;
            }

            author.revenue_balance_vnd += authorShare;

            var metadata = JsonSerializer.Serialize(new
            {
                chapterId = chapter.chapter_id,
                priceDias,
                grossVnd,
                rewardRate
            }, JsonOptions);

            await _chapterPurchaseRepository.AddAuthorRevenueTransactionAsync(new author_revenue_transaction
            {
                trans_id = Guid.NewGuid(),
                author_id = author.account_id,
                type = "purchase",
                amount_vnd = authorShare,
                purchase_log_id = purchaseId,
                metadata = metadata,
                created_at = now
            }, ct);

            await _chapterPurchaseRepository.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new ChapterPurchaseResponse
            {
                PurchaseId = purchaseId,
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                ChapterTitle = chapter.title,
                PriceDias = (int)priceDias,
                WalletBalanceAfter = wallet.balance_coin,
                AuthorShareVnd = authorShare,
                PurchasedAt = now
            };
        }
    }
}
