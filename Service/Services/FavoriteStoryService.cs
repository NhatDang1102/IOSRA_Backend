using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Story;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class FavoriteStoryService : IFavoriteStoryService
    {
        private readonly IFavoriteStoryRepository _favoriteRepository;
        private readonly IStoryCatalogRepository _storyRepository;

        public FavoriteStoryService(
            IFavoriteStoryRepository favoriteRepository,
            IStoryCatalogRepository storyRepository)
        {
            _favoriteRepository = favoriteRepository;
            _storyRepository = storyRepository;
        }

        public async Task<FavoriteStoryResponse> AddAsync(Guid readerId, Guid storyId, CancellationToken ct = default)
        {
            var existing = await _favoriteRepository.GetAsync(readerId, storyId, ct);
            if (existing != null)
            {
                return Map(existing);
            }

            var story = await _storyRepository.GetPublishedStoryByIdAsync(storyId, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found or unavailable.", 404);

            var entity = new favorite_story
            {
                reader_id = readerId,
                story_id = story.story_id,
                noti_new_chapter = true,
                created_at = TimezoneConverter.VietnamNow,
                story = story
            };

            await _favoriteRepository.AddAsync(entity, ct);
            await _favoriteRepository.SaveChangesAsync(ct);

            return Map(entity);
        }

        public async Task RemoveAsync(Guid readerId, Guid storyId, CancellationToken ct = default)
        {
            var existing = await _favoriteRepository.GetAsync(readerId, storyId, ct)
                           ?? throw new AppException("FavoriteNotFound", "Story is not in your favorite list.", 404);

            await _favoriteRepository.RemoveAsync(existing, ct);
            await _favoriteRepository.SaveChangesAsync(ct);
        }

        public async Task<FavoriteStoryResponse> ToggleNotificationAsync(Guid readerId, Guid storyId, FavoriteStoryNotificationRequest request, CancellationToken ct = default)
        {
            var existing = await _favoriteRepository.GetAsync(readerId, storyId, ct)
                           ?? throw new AppException("FavoriteNotFound", "Story is not in your favorite list.", 404);

            existing.noti_new_chapter = request?.Enabled ?? false;
            await _favoriteRepository.SaveChangesAsync(ct);
            return Map(existing);
        }

        public async Task<PagedResult<FavoriteStoryResponse>> ListAsync(Guid readerId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1 || pageSize < 1)
            {
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);
            }

            var (items, total) = await _favoriteRepository.ListAsync(readerId, page, pageSize, ct);
            var responses = items.Select(Map).ToArray();

            return new PagedResult<FavoriteStoryResponse>
            {
                Items = responses,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private static FavoriteStoryResponse Map(favorite_story entity)
        {
            var story = entity.story ?? throw new AppException("StoryNotLoaded", "Story info missing.", 500);
            var authorAccount = story.author?.account;

            return new FavoriteStoryResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                CoverUrl = story.cover_url,
                AuthorId = story.author_id,
                AuthorUsername = authorAccount?.username ?? string.Empty,
                NotiNewChapter = entity.noti_new_chapter,
                CreatedAt = entity.created_at
            };
        }
    }
}
