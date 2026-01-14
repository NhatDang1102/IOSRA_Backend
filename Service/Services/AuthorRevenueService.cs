using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.Common;
using Repository.DataModels;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using Service.Models;

namespace Service.Services
{
    public class AuthorRevenueService : IAuthorRevenueService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly string[] AllowedTypes = { "purchase", "withdraw_reserve", "withdraw_release", "voice_generation" };

        private readonly IAuthorRevenueRepository _repository;
        private readonly IOpRequestRepository _opRequestRepository;

        public AuthorRevenueService(
            IAuthorRevenueRepository repository,
            IOpRequestRepository opRequestRepository)
        {
            _repository = repository;
            _opRequestRepository = opRequestRepository;
        }

        public async Task<AuthorRevenueSummaryResponse> GetSummaryAsync(Guid authorAccountId, CancellationToken ct = default)
        {
            var author = await _repository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Không tìm thấy hồ sơ tác giả.", 404);

            var balance = author.revenue_balance;
            var pending = author.revenue_pending;
            var withdrawn = author.revenue_withdrawn;

            return new AuthorRevenueSummaryResponse
            {
                RevenueBalance = balance,
                RevenuePending = pending,
                RevenueWithdrawn = withdrawn,
                TotalRevenue = balance + pending + withdrawn,
                RankName = author.rank?.rank_name,
                RankRewardRate = author.rank?.reward_rate
            };
        }

        public async Task<PagedResult<AuthorRevenueTransactionItemResponse>> GetTransactionsAsync(Guid authorAccountId, AuthorRevenueTransactionQuery query, CancellationToken ct = default)
        {
            if (query.Page < 1)
            {
                throw new AppException("ValidationFailed", "Page phải lớn hơn 0.", 400);
            }

            if (query.PageSize < 1 || query.PageSize > 200)
            {
                throw new AppException("ValidationFailed", "PageSize phải nằm trong khoảng từ 1 đến 200.", 400);
            }

            var type = NormalizeType(query.Type);
            ValidateDateRange(query.From, query.To);

            _ = await _repository.GetAuthorAsync(authorAccountId, ct)
                ?? throw new AppException("AuthorProfileMissing", "Không tìm thấy hồ sơ tác giả.", 404);

            var (items, total) = await _repository.GetTransactionsAsync(authorAccountId, query.Page, query.PageSize, type, query.From, query.To, ct);

            var mapped = items.Select(t =>
            {
                var chapter = t.purchase_log?.chapter ?? t.voice_purchase?.chapter;
                var story = chapter?.story;
                var voiceNames = t.voice_purchase?.voice_purchase_items?
                    .Select(v => v.voice?.voice_name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var itemType = t.type;
                if (string.Equals(t.type, "purchase", StringComparison.OrdinalIgnoreCase))
                {
                    if (t.purchase_log_id.HasValue)
                    {
                        itemType = "chapter_purchase";
                    }
                    else if (t.voice_purchase_id.HasValue)
                    {
                        itemType = "voice_purchase";
                    }
                }

                return new AuthorRevenueTransactionItemResponse
                {
                    TransactionId = t.trans_id,
                    Type = itemType,
                    Amount = t.amount,
                    StoryId = story?.story_id,
                    StoryTitle = story?.title,
                    ChapterId = chapter?.chapter_id,
                    ChapterTitle = chapter?.title,
                    PurchaseLogId = t.purchase_log_id,
                    VoicePurchaseId = t.voice_purchase_id,
                    RequestId = t.request_id,
                    VoiceNames = voiceNames != null && voiceNames.Length > 0 ? voiceNames : null,
                    Metadata = ParseMetadata(t.metadata),
                    CreatedAt = t.created_at
                };
            }).ToList();

            return new PagedResult<AuthorRevenueTransactionItemResponse>
            {
                Items = mapped,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<AuthorWithdrawRequestResponse> SubmitWithdrawAsync(Guid authorAccountId, AuthorWithdrawRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("ValidationFailed", "Nội dung yêu cầu là bắt buộc.", 400);
            }

            var amount = request.Amount;
            if (amount < 1000)
            {
                throw new AppException("AmountTooSmall", "Số tiền rút tối thiểu là 1000 đơn vị.", 400);
            }

            var author = await _repository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Không tìm thấy hồ sơ tác giả.", 404);

            if (author.revenue_balance < amount)
            {
                throw new AppException("InsufficientRevenue", "Số dư doanh thu không đủ để rút.", 400);
            }

            if (await _opRequestRepository.HasPendingWithdrawRequestAsync(authorAccountId, ct))
            {
                throw new AppException("WithdrawPending", "Bạn đang có một yêu cầu đối soát đang chờ xử lý.", 409);
            }

            var payload = new AuthorWithdrawPayload
            {
                BankName = request.BankName.Trim(),
                BankAccountNumber = request.BankAccountNumber.Trim(),
                AccountHolderName = request.AccountHolderName.Trim(),
                Commitment = string.IsNullOrWhiteSpace(request.Commitment) ? null : request.Commitment.Trim()
            };

            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            var now = TimezoneConverter.VietnamNow;

            await using var transaction = await _repository.BeginTransactionAsync(ct);

            author.revenue_balance -= amount;
            author.revenue_pending += amount;

            var opRequest = await _opRequestRepository.CreateWithdrawRequestAsync(
                authorAccountId,
                payloadJson,
                (ulong)amount,
                ct);

            await _repository.AddTransactionAsync(new author_revenue_transaction
            {
                trans_id = Guid.NewGuid(),
                author_id = author.account_id,
                type = "withdraw_reserve",
                amount = -amount,
                request_id = opRequest.request_id,
                created_at = now,
                metadata = payloadJson
            }, ct);

            await _repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return MapWithdrawResponse(opRequest, payload);
        }

        public async Task ConfirmReceiptAsync(Guid authorAccountId, Guid requestId, CancellationToken ct = default)
        {
            var request = await _opRequestRepository.GetWithdrawRequestAsync(requestId, ct)
                          ?? throw new AppException("RequestNotFound", "Không tìm thấy yêu cầu đối soát.", 404);

            if (request.requester_id != authorAccountId)
            {
                throw new AppException("RequestNotFound", "Không tìm thấy yêu cầu đối soát.", 404);
            }

            if (!string.Equals(request.status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Chỉ yêu cầu đã được duyệt mới có thể xác nhận.", 400);
            }

            request.status = "confirmed";
            

            await _opRequestRepository.UpdateOpRequestAsync(request, ct);
        }

        public async Task<IReadOnlyList<AuthorWithdrawRequestResponse>> GetWithdrawRequestsAsync(Guid authorAccountId, string? status, CancellationToken ct = default)
        {
            var requests = await _opRequestRepository.ListWithdrawRequestsAsync(authorAccountId, status, ct);
            return requests.Select(MapWithdrawResponse).ToArray();
        }

        public async Task<ContentRevenueDetailResponse> GetStoryRevenueDetailAsync(Guid authorAccountId, Guid storyId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) throw new AppException("ValidationFailed", "Page phải lớn hơn 0.", 400);
            if (pageSize < 1 || pageSize > 200) throw new AppException("ValidationFailed", "PageSize phải nằm trong khoảng từ 1 đến 200.", 400);

            var story = await _repository.GetStoryOwnedByAuthorAsync(storyId, authorAccountId, ct);
            if (story == null)
            {
                throw new AppException("AccessDenied", "Bạn không có quyền truy cập thông tin doanh thu của truyện này.", 403);
            }

            var (items, total, totalRevenue, chapterRevenue, voiceRevenue) = await _repository.GetStoryPurchaseLogsAsync(storyId, page, pageSize, ct);

            return new ContentRevenueDetailResponse
            {
                ContentId = storyId,
                Title = story.title,
                TotalRevenue = totalRevenue,
                ChapterRevenue = chapterRevenue,
                VoiceRevenue = voiceRevenue,
                TotalPurchases = total,
                Purchasers = new PagedResult<PurchaserDetailDto>
                {
                    Items = items.Select(MapToPurchaserDto).ToList(),
                    Total = total,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }

        public async Task<ContentRevenueDetailResponse> GetChapterRevenueDetailAsync(Guid authorAccountId, Guid chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) throw new AppException("ValidationFailed", "Page phải lớn hơn 0.", 400);
            if (pageSize < 1 || pageSize > 200) throw new AppException("ValidationFailed", "PageSize phải nằm trong khoảng từ 1 đến 200.", 400);

            var chapter = await _repository.GetChapterOwnedByAuthorAsync(chapterId, authorAccountId, ct);
            if (chapter == null)
            {
                throw new AppException("AccessDenied", "Bạn không có quyền truy cập thông tin doanh thu của chương này.", 403);
            }

            var (items, total, totalRevenue, chapterRevenue, voiceRevenue) = await _repository.GetChapterPurchaseLogsAsync(chapterId, page, pageSize, ct);

            return new ContentRevenueDetailResponse
            {
                ContentId = chapterId,
                Title = chapter.title,
                TotalRevenue = totalRevenue,
                ChapterRevenue = chapterRevenue,
                VoiceRevenue = voiceRevenue,
                TotalPurchases = total,
                Purchasers = new PagedResult<PurchaserDetailDto>
                {
                    Items = items.Select(MapToPurchaserDto).ToList(),
                    Total = total,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }

        private static PurchaserDetailDto MapToPurchaserDto(RevenuePurchaseLogData log)
        {
            return new PurchaserDetailDto
            {
                AccountId = log.AccountId,
                Username = log.Username,
                AvatarUrl = log.AvatarUrl,
                Price = (int)log.Price,
                PurchaseDate = log.CreatedAt,
                Type = log.Type
            };
        }

        private static string? NormalizeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            var normalized = type.Trim().ToLowerInvariant();
            if (!AllowedTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("ValidationFailed", $"Loại '{type}' không được hỗ trợ. Các giá trị cho phép: {string.Join(", ", AllowedTypes)}", 400);
            }

            return normalized;
        }

        private static void ValidateDateRange(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                throw new AppException("ValidationFailed", "Ngày bắt đầu phải sớm hơn ngày kết thúc.", 400);
            }
        }

        private static object? ParseMetadata(string? metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<object>(metadata, JsonOptions);
            }
            catch
            {
                return metadata;
            }
        }

        private static AuthorWithdrawRequestResponse MapWithdrawResponse(op_request request)
        {
            var payload = ParseWithdrawPayload(request);
            return MapWithdrawResponse(request, payload);
        }

        private static AuthorWithdrawRequestResponse MapWithdrawResponse(op_request request, AuthorWithdrawPayload payload)
        {
            var amount = request.withdraw_amount.HasValue ? (long)request.withdraw_amount.Value : 0L;
            return new AuthorWithdrawRequestResponse
            {
                RequestId = request.request_id,
                Amount = amount,
                Status = request.status,
                BankName = payload.BankName,
                BankAccountNumber = payload.BankAccountNumber,
                AccountHolderName = payload.AccountHolderName,
                Commitment = payload.Commitment,
                ModeratorNote = request.omod_note,
                TransactionCode = request.withdraw_code,
                ModeratorUsername = request.omod?.account?.username,
                CreatedAt = request.created_at,
                ReviewedAt = request.reviewed_at
            };
        }

        private static AuthorWithdrawPayload ParseWithdrawPayload(op_request request)
        {
            if (string.IsNullOrWhiteSpace(request.request_content))
            {
                return new AuthorWithdrawPayload();
            }

            try
            {
                return JsonSerializer.Deserialize<AuthorWithdrawPayload>(request.request_content, JsonOptions)
                       ?? new AuthorWithdrawPayload();
            }
            catch
            {
                return new AuthorWithdrawPayload();
            }
        }
    }
}