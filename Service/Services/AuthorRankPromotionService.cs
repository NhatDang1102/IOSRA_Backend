using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Respond.Author;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class AuthorRankPromotionService : IAuthorRankPromotionService
    {
        private readonly IAuthorRankUpgradeRepository _repository;
        private readonly INotificationService _notificationService;

        public AuthorRankPromotionService(
            IAuthorRankUpgradeRepository repository,
            INotificationService notificationService)
        {
            _repository = repository;
            _notificationService = notificationService;
        }

        public async Task<RankPromotionRequestResponse> SubmitAsync(Guid authorAccountId, RankPromotionSubmitRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("InvalidRequest", "Request body is required.", 400);
            }

            var fullName = request.FullName?.Trim();
            var commitment = request.Commitment?.Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new AppException("FullNameRequired", "Full name is required.", 400);
            }

            if (string.IsNullOrWhiteSpace(commitment))
            {
                throw new AppException("CommitmentRequired", "Commitment content is required.", 400);
            }

            var author = await _repository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Author profile was not found.", 404);

            if (author.rank_id == null)
            {
                throw new AppException("RankMissing", "Author rank is not assigned. Please contact support.", 409);
            }

            if (!await _repository.HasPublishedStoryAsync(authorAccountId, ct))
            {
                throw new AppException("StoryRequirement", "You must have at least one published story to request a promotion.", 400);
            }

            if (await _repository.HasPendingRequestAsync(authorAccountId, ct))
            {
                throw new AppException("PendingExists", "You already have a pending rank promotion request.", 409);
            }

            var ranks = await _repository.GetAllRanksAsync(ct);
            if (ranks.Count == 0)
            {
                throw new AppException("RankSeedMissing", "Author ranks have not been seeded.", 500);
            }

            var orderedRanks = ranks.OrderBy(r => r.min_followers).ToList();
            var currentIndex = orderedRanks.FindIndex(r => r.rank_id == author.rank_id);
            if (currentIndex < 0)
            {
                throw new AppException("RankUnknown", "Current rank is not recognized.", 500);
            }

            if (currentIndex >= orderedRanks.Count - 1)
            {
                throw new AppException("HighestRank", "You already reached the highest rank.", 400);
            }

            var nextRank = orderedRanks[currentIndex + 1];
            if (author.total_follower < nextRank.min_followers)
            {
                throw new AppException("FollowerRequirement", $"You need at least {nextRank.min_followers} followers to request {nextRank.rank_name}.", 400);
            }

            var created = await _repository.CreateAsync(new author_rank_upgrade_request
            {
                author_id = authorAccountId,
                current_rank_id = author.rank_id,
                target_rank_id = nextRank.rank_id,
                full_name = fullName,
                commitment = commitment
            }, ct);

            created.author = author;
            created.target_rank = nextRank;

            return Map(created);
        }

        public async Task<IReadOnlyList<RankPromotionRequestResponse>> ListMineAsync(Guid authorAccountId, CancellationToken ct = default)
        {
            var items = await _repository.GetRequestsByAuthorAsync(authorAccountId, ct);
            return items.Select(Map).ToArray();
        }

        public async Task<IReadOnlyList<RankPromotionRequestResponse>> ListForModerationAsync(string? status, CancellationToken ct = default)
        {
            var items = await _repository.ListAsync(status, ct);
            return items.Select(Map).ToArray();
        }

        public async Task ApproveAsync(Guid requestId, Guid omodAccountId, string? note, CancellationToken ct = default)
        {
            var request = await _repository.GetByIdAsync(requestId, ct)
                          ?? throw new AppException("RankRequestNotFound", "Rank promotion request was not found.", 404);

            if (!string.Equals(request.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be approved.", 400);
            }

            await _repository.UpdateAuthorRankAsync(request.author_id, request.target_rank_id, ct);

            request.status = "approved";
            request.omod_id = omodAccountId;
            request.reviewed_at = TimezoneConverter.VietnamNow;
            if (!string.IsNullOrWhiteSpace(note))
            {
                request.mod_note = note.Trim();
            }
            await _repository.UpdateAsync(request, ct);

            await _notificationService.CreateAsync(new NotificationCreateModel(
                request.author_id,
                NotificationTypes.AuthorRankUpgrade,
                $"Yêu cầu nâng hạng {request.target_rank?.rank_name ?? "mới"} đã được duyệt",
                $"Chúc mừng! Bạn đã được nâng lên hạng {request.target_rank?.rank_name ?? "mới"}.",
                new
                {
                    requestId = request.request_id,
                    status = request.status,
                    targetRank = request.target_rank?.rank_name
                }), ct);
        }

        public async Task RejectAsync(Guid requestId, Guid omodAccountId, RankPromotionRejectRequest request, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(requestId, ct)
                         ?? throw new AppException("RankRequestNotFound", "Rank promotion request was not found.", 404);

            if (!string.Equals(entity.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be rejected.", 400);
            }

            var reason = request?.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new AppException("ReasonRequired", "Rejection reason is required.", 400);
            }

            entity.status = "rejected";
            entity.omod_id = omodAccountId;
            entity.reviewed_at = TimezoneConverter.VietnamNow;
            entity.mod_note = reason;
            await _repository.UpdateAsync(entity, ct);

            await _notificationService.CreateAsync(new NotificationCreateModel(
                entity.author_id,
                NotificationTypes.AuthorRankUpgrade,
                "Yêu cầu nâng hạng bị từ chối",
                $"Yêu cầu nâng hạng của bạn đã bị từ chối: {reason}",
                new
                {
                    requestId = entity.request_id,
                    status = entity.status,
                    reason
                }), ct);
        }

        private static RankPromotionRequestResponse Map(author_rank_upgrade_request entity)
        {
            var authorAccount = entity.author?.account;
            return new RankPromotionRequestResponse
            {
                RequestId = entity.request_id,
                AuthorId = entity.author_id,
                AuthorUsername = authorAccount?.username ?? string.Empty,
                AuthorEmail = authorAccount?.email ?? string.Empty,
                FullName = entity.full_name,
                Commitment = entity.commitment,
                CurrentRankName = entity.author?.rank?.rank_name,
                TargetRankName = entity.target_rank?.rank_name ?? string.Empty,
                TargetRankMinFollowers = entity.target_rank?.min_followers ?? 0,
                TotalFollowers = entity.author?.total_follower ?? 0,
                Status = entity.status,
                ModeratorId = entity.omod_id,
                ModeratorUsername = entity.moderator?.username,
                ModeratorNote = entity.mod_note,
                CreatedAt = entity.created_at,
                ReviewedAt = entity.reviewed_at
            };
        }
    }
}
