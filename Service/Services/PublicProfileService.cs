using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Contract.DTOs.Respond.Profile;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Service.Services
{
    public class PublicProfileService : IPublicProfileService
    {
        private readonly IPublicProfileRepository _publicProfileRepository;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public PublicProfileService(IPublicProfileRepository publicProfileRepository, IMemoryCache cache)
        {
            _publicProfileRepository = publicProfileRepository;
            _cache = cache;
        }

        public async Task<PublicProfileResponse> GetAsync(Guid viewerAccountId, Guid targetAccountId, CancellationToken ct = default)
        {
            if (targetAccountId == Guid.Empty)
            {
                throw new AppException("ValidationFailed", "Target account id is required.", 400);
            }

            var cacheKey = $"profile:public:{targetAccountId:N}";
            if (_cache.TryGetValue(cacheKey, out PublicProfileResponse? cached) && cached is not null)
            {
                return cached;
            }

            var projection = await _publicProfileRepository.GetPublicProfileAsync(targetAccountId, ct)
                             ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            if (string.Equals(projection.Status, "banned", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("AccountUnavailable", "Account is not available.", 404);
            }

            var mapped = Map(projection);
            _cache.Set(cacheKey, mapped, CacheDuration);
            return mapped;
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

