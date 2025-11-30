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

namespace Repository.Repositories
{
    public class MoodMusicRepository : BaseRepository, IMoodMusicRepository
    {
        public MoodMusicRepository(AppDbContext db) : base(db)
        {
        }

        public Task<IReadOnlyList<chapter_mood>> GetMoodsAsync(CancellationToken ct = default)
            => _db.chapter_moods
                .AsNoTracking()
                .OrderBy(m => m.mood_name)
                .ToListAsync(ct)
                .ContinueWith(t => (IReadOnlyList<chapter_mood>)t.Result, ct);

        public Task<chapter_mood?> GetMoodAsync(string moodCode, CancellationToken ct = default)
            => _db.chapter_moods.FirstOrDefaultAsync(m => m.mood_code == moodCode, ct);

        public Task<chapter_mood_track?> GetTrackAsync(Guid trackId, CancellationToken ct = default)
            => _db.chapter_mood_tracks.FirstOrDefaultAsync(t => t.track_id == trackId, ct);

        public async Task<IReadOnlyList<chapter_mood_track>> GetTracksAsync(string? moodCode, CancellationToken ct = default)
        {
            var query = _db.chapter_mood_tracks
                .AsNoTracking()
                .Include(t => t.mood_codeNavigation)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(moodCode))
            {
                query = query.Where(t => t.mood_code == moodCode);
            }

            return await query
                .OrderByDescending(t => t.created_at)
                .ToListAsync(ct);
        }

        public Task<chapter_mood_track?> GetRandomTrackAsync(string moodCode, CancellationToken ct = default)
            => _db.chapter_mood_tracks
                .AsNoTracking()
                .Where(t => t.mood_code == moodCode)
                .OrderBy(_ => EF.Functions.Random())
                .FirstOrDefaultAsync(ct);

        public Task<IReadOnlyList<chapter_mood_track>> GetTracksByMoodAsync(string moodCode, CancellationToken ct = default)
            => _db.chapter_mood_tracks
                .AsNoTracking()
                .Where(t => t.mood_code == moodCode)
                .OrderByDescending(t => t.created_at)
                .ToListAsync(ct)
                .ContinueWith(t => (IReadOnlyList<chapter_mood_track>)t.Result, ct);

        public void AddTrack(chapter_mood_track track) => _db.chapter_mood_tracks.Add(track);

        public void RemoveTrack(chapter_mood_track track) => _db.chapter_mood_tracks.Remove(track);

        public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
