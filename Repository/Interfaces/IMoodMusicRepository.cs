using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IMoodMusicRepository
    {
        Task<IReadOnlyList<chapter_mood>> GetMoodsAsync(CancellationToken ct = default);
        Task<chapter_mood?> GetMoodAsync(string moodCode, CancellationToken ct = default);
        Task<chapter_mood_track?> GetTrackAsync(Guid trackId, CancellationToken ct = default);
        Task<IReadOnlyList<chapter_mood_track>> GetTracksAsync(string? moodCode, CancellationToken ct = default);
        Task<chapter_mood_track?> GetRandomTrackAsync(string moodCode, CancellationToken ct = default);
        Task<IReadOnlyList<chapter_mood_track>> GetTracksByMoodAsync(string moodCode, CancellationToken ct = default);
        void AddTrack(chapter_mood_track track);
        void RemoveTrack(chapter_mood_track track);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
