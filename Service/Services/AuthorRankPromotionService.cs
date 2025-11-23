using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IAuthorStoryRepository _authorRepository;
        private readonly IOpRequestRepository _opRequestRepository;
        private readonly INotificationService _notificationService;

        public AuthorRankPromotionService(
            IAuthorStoryRepository authorRepository,
            IOpRequestRepository opRequestRepository,
            INotificationService notificationService)
        {
            _authorRepository = authorRepository;
            _opRequestRepository = opRequestRepository;
            _notificationService = notificationService;
        }

        public async Task<RankPromotionRequestResponse> SubmitAsync(Guid authorAccountId, RankPromotionSubmitRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("InvalidRequest", "Request body is required.", 400);
            }

            var commitment = request.Commitment?.Trim();
            if (string.IsNullOrWhiteSpace(commitment))
            {
                throw new AppException("CommitmentRequired", "Commitment content is required.", 400);
            }

            var author = await _authorRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Author profile was not found.", 404);

            if (author.rank_id == null)
            {
                throw new AppException("RankMissing", "Author rank is not assigned. Please contact support.", 409);
            }

            if (!await _authorRepository.AuthorHasPublishedStoryAsync(authorAccountId, ct))
            {
                throw new AppException("StoryRequirement", "You must have at least one published story to request a promotion.", 400);
            }

            if (await _opRequestRepository.HasPendingRankPromotionRequestAsync(authorAccountId, ct))
            {
                throw new AppException("PendingExists", "You already have a pending rank promotion request.", 409);
            }

            var ranks = await _authorRepository.GetAllRanksAsync(ct);
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

            var payload = new RankPromotionPayload
            {
                Commitment = commitment,
                CurrentRankId = author.rank_id,
                CurrentRankName = author.rank?.rank_name,
                TargetRankId = nextRank.rank_id,
                TargetRankName = nextRank.rank_name,
                TargetRankMinFollowers = nextRank.min_followers,
                SubmittedFollowerCount = author.total_follower
            };

            var created = await _opRequestRepository.CreateRankPromotionRequestAsync(
                authorAccountId,
                JsonSerializer.Serialize(payload, JsonOptions),
                ct);

            return Map(created, payload);
        }

        public async Task<IReadOnlyList<RankPromotionRequestResponse>> ListMineAsync(Guid authorAccountId, CancellationToken ct = default)
        {
            var requests = await _opRequestRepository.ListRankPromotionRequestsAsync(authorAccountId, null, ct);
            return requests.Select(Map).ToArray();
        }

        public async Task<IReadOnlyList<RankPromotionRequestResponse>> ListForModerationAsync(string? status, CancellationToken ct = default)
        {
            var requests = await _opRequestRepository.ListRankPromotionRequestsAsync(null, status, ct);
            return requests.Select(Map).ToArray();
        }

        public async Task ApproveAsync(Guid requestId, Guid omodAccountId, string? note, CancellationToken ct = default)
        {
            var request = await _opRequestRepository.GetRankPromotionRequestAsync(requestId, ct)
                          ?? throw new AppException("RankRequestNotFound", "Rank promotion request was not found.", 404);

            if (!string.Equals(request.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be approved.", 400);
            }

            var payload = ParsePayload(request);

            await _authorRepository.UpdateAuthorRankAsync(request.requester_id, payload.TargetRankId, ct);

            request.status = "approved";
            request.omod_id = omodAccountId;
            request.reviewed_at = TimezoneConverter.VietnamNow;
            request.omod_note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            await _opRequestRepository.UpdateAsync(request, ct);

            await _notificationService.CreateAsync(new NotificationCreateModel(
                request.requester_id,
                NotificationTypes.AuthorRankUpgrade,
                $"Yêu cầu nâng hạng {payload.TargetRankName} đã được duyệt",
                $"Chúc mừng! Bạn đã được nâng lên hạng {payload.TargetRankName}.",
                new
                {
                    requestId = request.request_id,
                    status = request.status,
                    targetRank = payload.TargetRankName
                }), ct);
        }

        public async Task RejectAsync(Guid requestId, Guid omodAccountId, RankPromotionRejectRequest request, CancellationToken ct = default)
        {
            var entity = await _opRequestRepository.GetRankPromotionRequestAsync(requestId, ct)
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
            entity.omod_note = reason;

            await _opRequestRepository.UpdateAsync(entity, ct);

            var payload = ParsePayload(entity);

            await _notificationService.CreateAsync(new NotificationCreateModel(
                entity.requester_id,
                NotificationTypes.AuthorRankUpgrade,
                "Yêu cầu nâng hạng bị từ chối",
                $"Yêu cầu nâng hạng {payload.TargetRankName} của bạn đã bị từ chối: {reason}",
                new
                {
                    requestId = entity.request_id,
                    status = entity.status,
                    reason
                }), ct);
        }

        private static RankPromotionRequestResponse Map(op_request request)
        {
            var payload = ParsePayload(request);
            return Map(request, payload);
        }

        private static RankPromotionRequestResponse Map(op_request request, RankPromotionPayload payload)
        {
            var requester = request.requester;
            return new RankPromotionRequestResponse
            {
                RequestId = request.request_id,
                AuthorId = request.requester_id,
                AuthorUsername = requester?.username ?? string.Empty,
                AuthorEmail = requester?.email ?? string.Empty,
                Commitment = payload.Commitment,
                CurrentRankName = payload.CurrentRankName,
                TargetRankName = payload.TargetRankName,
                TargetRankMinFollowers = payload.TargetRankMinFollowers,
                TotalFollowers = payload.SubmittedFollowerCount,
                Status = request.status,
                ModeratorId = request.omod_id,
                ModeratorUsername = request.omod?.account.username,
                ModeratorNote = request.omod_note,
                CreatedAt = request.created_at,
                ReviewedAt = request.reviewed_at
            };
        }

        private static RankPromotionPayload ParsePayload(op_request request)
        {
            if (string.IsNullOrWhiteSpace(request.request_content))
            {
                throw new AppException("CorruptedRequestPayload", "Rank promotion request payload is invalid.", 500);
            }

            try
            {
                return JsonSerializer.Deserialize<RankPromotionPayload>(request.request_content, JsonOptions)
                       ?? throw new JsonException("Empty payload");
            }
            catch (Exception ex)
            {
                throw new AppException("CorruptedRequestPayload", "Rank promotion request payload is invalid.", 500, ex);
            }
        }

        private sealed class RankPromotionPayload
        {
            public string Commitment { get; set; } = string.Empty;
            public Guid? CurrentRankId { get; set; }
            public string? CurrentRankName { get; set; }
            public Guid TargetRankId { get; set; }
            public string TargetRankName { get; set; } = string.Empty;
            public uint TargetRankMinFollowers { get; set; }
            public uint SubmittedFollowerCount { get; set; }
        }
    }
}
