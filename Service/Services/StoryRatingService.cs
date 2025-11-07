using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Story;
using Repository.Entities;
using Repository.Interfaces;
using Repository.DataModels;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class StoryRatingService : IStoryRatingService
    {
        private const int MaxPageSize = 100;

        private readonly IStoryRatingRepository _ratingRepository;
        private readonly IStoryCatalogRepository _storyCatalogRepository;
        private readonly IProfileRepository _profileRepository;

        public StoryRatingService(
            IStoryRatingRepository ratingRepository,
            IStoryCatalogRepository storyCatalogRepository,
            IProfileRepository profileRepository)
        {
            _ratingRepository = ratingRepository;
            _storyCatalogRepository = storyCatalogRepository;
            _profileRepository = profileRepository;
        }

        public async Task<StoryRatingSummaryResponse> GetAsync(Guid storyId, Guid? viewerId, int page, int pageSize, CancellationToken ct = default)
        {
            var story = await RequirePublishedStoryAsync(storyId, ct);
            var normalizedPage = NormalizePage(page);
            var normalizedPageSize = NormalizePageSize(pageSize);

            var (items, total) = await _ratingRepository.GetRatingsPageAsync(story.story_id, normalizedPage, normalizedPageSize, ct);
            var summaryData = await _ratingRepository.GetSummaryAsync(story.story_id, ct);

            StoryRatingItemResponse? viewerRating = null;
            if (viewerId.HasValue && viewerId.Value != Guid.Empty)
            {
                var viewerEntity = await _ratingRepository.GetDetailsAsync(story.story_id, viewerId.Value, ct);
                if (viewerEntity != null)
                {
                    viewerRating = MapRating(viewerEntity);
                }
            }

            return new StoryRatingSummaryResponse
            {
                StoryId = story.story_id,
                AverageScore = summaryData.AverageScore,
                TotalRatings = summaryData.TotalRatings,
                Distribution = new Dictionary<byte, int>(summaryData.Distribution),
                ViewerRating = viewerRating,
                Ratings = new PagedResult<StoryRatingItemResponse>
                {
                    Items = items.Select(MapRating).ToArray(),
                    Total = total,
                    Page = normalizedPage,
                    PageSize = normalizedPageSize
                }
            };
        }

        public async Task<StoryRatingItemResponse> UpsertAsync(Guid readerAccountId, Guid storyId, StoryRatingRequest request, CancellationToken ct = default)
        {
            var story = await RequirePublishedStoryAsync(storyId, ct);
            var reader = await _profileRepository.GetReaderByIdAsync(readerAccountId, ct)
                         ?? throw new AppException("ReaderProfileMissing", "Reader profile is not registered.", 404);

            var rating = await _ratingRepository.GetAsync(story.story_id, reader.account_id, ct);
            var now = TimezoneConverter.VietnamNow;
            if (rating == null)
            {
                rating = new story_rating
                {
                    story_id = story.story_id,
                    reader_id = reader.account_id,
                    score = request.Score,
                    created_at = now,
                    updated_at = now
                };
                await _ratingRepository.AddAsync(rating, ct);
            }
            else
            {
                rating.score = request.Score;
                rating.updated_at = now;
                await _ratingRepository.UpdateAsync(rating, ct);
            }

            var persisted = await _ratingRepository.GetDetailsAsync(story.story_id, reader.account_id, ct)
                            ?? throw new InvalidOperationException("Failed to load rating after save.");
            return MapRating(persisted);
        }

        public async Task RemoveAsync(Guid readerAccountId, Guid storyId, CancellationToken ct = default)
        {
            await RequirePublishedStoryAsync(storyId, ct);
            var rating = await _ratingRepository.GetAsync(storyId, readerAccountId, ct)
                         ?? throw new AppException("RatingNotFound", "Rating was not found.", 404);
            await _ratingRepository.DeleteAsync(rating, ct);
        }

        private static StoryRatingItemResponse MapRating(story_rating rating)
        {
            var account = rating.reader?.account
                          ?? throw new InvalidOperationException("Rating reader account navigation not loaded.");

            return new StoryRatingItemResponse
            {
                ReaderId = rating.reader_id,
                Username = account.username,
                AvatarUrl = account.avatar_url,
                Score = rating.score,
                RatedAt = rating.created_at,
                UpdatedAt = rating.updated_at
            };
        }

        private async Task<story> RequirePublishedStoryAsync(Guid storyId, CancellationToken ct)
        {
            var story = await _storyCatalogRepository.GetPublishedStoryByIdAsync(storyId, ct);
            if (story == null)
            {
                throw new AppException("StoryNotFound", "Story was not found or cannot be rated.", 404);
            }
            return story;
        }

        private static int NormalizePage(int page) => page <= 0 ? 1 : page;
        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0) return 20;
            return pageSize > MaxPageSize ? MaxPageSize : pageSize;
        }
    }
}
