using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Contract.DTOs.Request.Follow;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Follow;
using Microsoft.Extensions.Caching.Memory;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class AuthorFollowService : IAuthorFollowService
    {
        private const int MaxPageSize = 100;

        private readonly IAuthorFollowRepository _followRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly IAuthorStoryRepository _authorStoryRepository;
        private readonly INotificationService _notificationService;
        private readonly IMemoryCache _cache;

        public AuthorFollowService(
            IAuthorFollowRepository followRepository,
            IProfileRepository profileRepository,
            IAuthorStoryRepository authorStoryRepository,
            INotificationService notificationService,
            IMemoryCache cache)
        {
            _followRepository = followRepository;
            _profileRepository = profileRepository;
            _authorStoryRepository = authorStoryRepository;
            _notificationService = notificationService;
            _cache = cache;
        }

        public async Task<AuthorFollowStatusResponse> FollowAsync(Guid readerAccountId, Guid authorAccountId, AuthorFollowRequest request, CancellationToken ct = default)
        {
            if (readerAccountId == authorAccountId)
            {
                throw new AppException("FollowSelfNotAllowed", "You cannot follow yourself.", 400);
            }

            var reader = await RequireReaderAsync(readerAccountId, ct);
            var author = await RequireAuthorAsync(authorAccountId, ct);

            var notifications = request?.EnableNotifications ?? true;
            var existing = await _followRepository.GetAsync(reader.account_id, author.account_id, ct);
            if (existing != null)
            {
                if ((existing.noti_new_story ?? true) != notifications)
                {
                    existing = await _followRepository.UpdateNotificationAsync(existing, notifications, ct);
                }

                return MapStatus(existing);
            }

            var entity = await _followRepository.AddAsync(reader.account_id, author.account_id, notifications, ct);
            IncrementFollowerCount(author, +1);
            await _authorStoryRepository.SaveChangesAsync(ct);
            
            _cache.Remove($"profile:public:{author.account_id:N}");

            await _notificationService.CreateAsync(new NotificationCreateModel(
                author.account_id,
                NotificationTypes.NewFollower,
                "Bạn có follower mới",
                $"{reader.account.username} vừa follow bạn.",
                new
                {
                    followerId = reader.account_id,
                    followerUsername = reader.account.username
                }), ct);

            return MapStatus(entity);
        }

        public async Task UnfollowAsync(Guid readerAccountId, Guid authorAccountId, CancellationToken ct = default)
        {
            var reader = await RequireReaderAsync(readerAccountId, ct);
            var author = await RequireAuthorAsync(authorAccountId, ct);

            var existing = await _followRepository.GetAsync(reader.account_id, author.account_id, ct)
                          ?? throw new AppException("FollowNotFound", "You are not following this author.", 404);

            await _followRepository.RemoveAsync(existing, ct);
            IncrementFollowerCount(author, -1);
            await _authorStoryRepository.SaveChangesAsync(ct);
            
            _cache.Remove($"profile:public:{author.account_id:N}");
        }

        public async Task<AuthorFollowStatusResponse> UpdateNotificationAsync(Guid readerAccountId, Guid authorAccountId, bool enableNotifications, CancellationToken ct = default)
        {
            var reader = await RequireReaderAsync(readerAccountId, ct);
            var author = await RequireAuthorAsync(authorAccountId, ct);

            var existing = await _followRepository.GetAsync(reader.account_id, author.account_id, ct)
                          ?? throw new AppException("FollowNotFound", "You are not following this author.", 404);

            if ((existing.noti_new_story ?? true) == enableNotifications)
            {
                return MapStatus(existing);
            }

            var updated = await _followRepository.UpdateNotificationAsync(existing, enableNotifications, ct);
            return MapStatus(updated);
        }

        public async Task<PagedResult<AuthorFollowerResponse>> GetFollowersAsync(Guid authorAccountId, int page, int pageSize, CancellationToken ct = default)
        {
            await RequireAuthorAsync(authorAccountId, ct);

            var normalizedPage = page <= 0 ? 1 : page;
            var normalizedSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);

            var (items, total) = await _followRepository.GetFollowersAsync(authorAccountId, normalizedPage, normalizedSize, ct);

            return new PagedResult<AuthorFollowerResponse>
            {
                Items = items.Select(MapFollower).ToArray(),
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task<PagedResult<AuthorFollowingResponse>> GetFollowingAsync(Guid readerAccountId, int page, int pageSize, CancellationToken ct = default)
        {
            await RequireReaderAsync(readerAccountId, ct);

            var normalizedPage = page <= 0 ? 1 : page;
            var normalizedSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);

            var (items, total) = await _followRepository.GetFollowingAsync(readerAccountId, normalizedPage, normalizedSize, ct);

            return new PagedResult<AuthorFollowingResponse>
            {
                Items = items.Select(MapFollowing).ToArray(),
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        private async Task<reader> RequireReaderAsync(Guid accountId, CancellationToken ct)
        {
            var reader = await _profileRepository.GetReaderByIdAsync(accountId, ct);
            if (reader == null)
            {
                throw new AppException("ReaderProfileMissing", "Reader profile is not registered.", 404);
            }
            return reader;
        }

        private async Task<author> RequireAuthorAsync(Guid accountId, CancellationToken ct)
        {
            var author = await _authorStoryRepository.GetAuthorAsync(accountId, ct);
            if (author == null)
            {
                throw new AppException("AuthorNotFound", "Author profile was not found.", 404);
            }
            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "This author is currently restricted.", 403);
            }
            return author;
        }

        private static AuthorFollowStatusResponse MapStatus(follow entity)
        {
            return new AuthorFollowStatusResponse
            {
                IsFollowing = true,
                NotificationsEnabled = entity.noti_new_story ?? true,
                FollowedAt = entity.created_at
            };
        }

        private static AuthorFollowerResponse MapFollower(AuthorFollowerProjection projection)
        {
            return new AuthorFollowerResponse
            {
                FollowerId = projection.FollowerId,
                Username = projection.Username,
                AvatarUrl = projection.AvatarUrl,
                NotificationsEnabled = projection.NotificationsEnabled,
                FollowedAt = projection.FollowedAt
            };
        }

        private static AuthorFollowingResponse MapFollowing(AuthorFollowingProjection projection)
        {
            return new AuthorFollowingResponse
            {
                AuthorId = projection.AuthorId,
                Username = projection.Username,
                AvatarUrl = projection.AvatarUrl,
                NotificationsEnabled = projection.NotificationsEnabled,
                FollowedAt = projection.FollowedAt
            };
        }

        private static void IncrementFollowerCount(author author, int delta)
        {
            var current = (int)author.total_follower;
            var updated = current + delta;
            if (updated < 0)
            {
                updated = 0;
            }

            author.total_follower = (uint)updated;
        }
    }
}
