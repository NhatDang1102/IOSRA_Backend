using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using Repository.DataModels;

namespace Repository.Repositories
{
    public class StoryRatingRepository : BaseRepository, IStoryRatingRepository
    {
        public StoryRatingRepository(AppDbContext db) : base(db)
        {
        }

        public Task<story_rating?> GetAsync(Guid storyId, Guid readerId, CancellationToken ct = default)
            => _db.story_ratings
                  .FirstOrDefaultAsync(r => r.story_id == storyId && r.reader_id == readerId, ct);

        public Task<story_rating?> GetDetailsAsync(Guid storyId, Guid readerId, CancellationToken ct = default)
            => _db.story_ratings
                  .AsNoTracking()
                  .Include(r => r.reader).ThenInclude(rd => rd.account)
                  .FirstOrDefaultAsync(r => r.story_id == storyId && r.reader_id == readerId, ct);

        public async Task AddAsync(story_rating rating, CancellationToken ct = default)
        {
            _db.story_ratings.Add(rating);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(story_rating rating, CancellationToken ct = default)
        {
            _db.story_ratings.Update(rating);
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(story_rating rating, CancellationToken ct = default)
        {
            _db.story_ratings.Remove(rating);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<(List<story_rating> Items, int Total)> GetRatingsPageAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.story_ratings
                .AsNoTracking()
                .Include(r => r.reader).ThenInclude(rd => rd.account)
                .Where(r => r.story_id == storyId);

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(r => r.updated_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<StoryRatingSummaryData> GetSummaryAsync(Guid storyId, CancellationToken ct = default)
        {
            var query = _db.story_ratings.AsNoTracking().Where(r => r.story_id == storyId);

            var total = await query.CountAsync(ct);
            decimal? average = null;
            if (total > 0)
            {
                var avgValue = await query.AverageAsync(r => (double)r.score, ct);
                average = Math.Round((decimal)avgValue, 2, MidpointRounding.AwayFromZero);
            }

            var distributionData = await query
                .GroupBy(r => r.score)
                .Select(g => new { Score = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var distribution = new Dictionary<byte, int>();
            for (byte score = 1; score <= 5; score++)
            {
                var entry = distributionData.FirstOrDefault(d => d.Score == score);
                distribution[score] = entry?.Count ?? 0;
            }

            return new StoryRatingSummaryData(storyId, average, total, distribution);
        }
    }
}
