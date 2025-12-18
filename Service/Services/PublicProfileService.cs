using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Contract.DTOs.Response.Profile;
using Microsoft.Extensions.Caching.Memory;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class PublicProfileService : IPublicProfileService
    {
        private readonly IPublicProfileRepository _publicProfileRepository;
        private readonly IAuthorFollowRepository _authorFollowRepository;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public PublicProfileService(IPublicProfileRepository publicProfileRepository, IAuthorFollowRepository authorFollowRepository, IMemoryCache cache)
        {
            _publicProfileRepository = publicProfileRepository;
            _authorFollowRepository = authorFollowRepository;
            _cache = cache;
        }

        public async Task<PublicProfileResponse> GetAsync(Guid viewerAccountId, Guid targetAccountId, CancellationToken ct = default)
        {
            if (targetAccountId == Guid.Empty)
            {
                throw new AppException("ValidationFailed", "ID tài khoản mục tiêu là bắt buộc.", 400);
            }

            var projection = await GetProjectionAsync(targetAccountId, ct);
            var response = Map(projection);

            if (viewerAccountId != Guid.Empty &&
                viewerAccountId != projection.AccountId &&
                projection.IsAuthor)
            {
                var follow = await _authorFollowRepository.GetAsync(viewerAccountId, projection.AccountId, ct);
                if (follow != null)
                {
                    response.FollowState = new FollowStateResponse
                    {
                        IsFollowing = true,
                        NotificationsEnabled = follow.noti_new_story ?? true,
                        FollowedAt = follow.created_at
                    };
                }
            }

            return response;
        }

        private async Task<PublicProfileProjection> GetProjectionAsync(Guid targetAccountId, CancellationToken ct)
        {
            var cacheKey = $"profile:public:{targetAccountId:N}";
            if (_cache.TryGetValue(cacheKey, out PublicProfileProjection? cached) && cached is not null)
            {
                return cached;
            }

            var projection = await _publicProfileRepository.GetPublicProfileAsync(targetAccountId, ct)
                             ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);

            if (string.Equals(projection.Status, "banned", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("AccountUnavailable", "Tài khoản không khả dụng.", 404);
            }

            _cache.Set(cacheKey, projection, CacheDuration);
            return projection;
        }

        private static PublicProfileResponse Map(PublicProfileProjection projection)
        {
            var response = new PublicProfileResponse
            {
                AccountId = projection.AccountId,
                Username = projection.Username,
                AvatarUrl = projection.AvatarUrl,
                Bio = projection.Bio,
                Gender = NormalizeGender(projection.Gender),
                CreatedAt = projection.CreatedAt,
                IsAuthor = projection.IsAuthor
            };

            if (projection.IsAuthor)
            {
                response.Author = new AuthorPublicProfileResponse
                {
                    AuthorId = projection.AccountId,
                    RankName = projection.AuthorRankName,
                    RankRewardRate = projection.AuthorRankRewardRate,
                    RankMinFollowers = projection.AuthorRankMinFollowers,
                    IsRestricted = projection.AuthorRestricted,
                    IsVerified = projection.AuthorVerified,
                    FollowerCount = projection.FollowerCount,
                    PublishedStoryCount = projection.PublishedStoryCount,
                    LatestPublishedAt = projection.LatestPublishedAt
                };
            }

            return response;
        }

        private static string NormalizeGender(string? dbValue)
        {
            return dbValue?.ToLowerInvariant() switch
            {
                "male" => "M",
                "female" => "F",
                "other" => "other",
                "unspecified" or null => "unspecified",
                _ => "unspecified"
            };
        }
    }
}