using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.ContentMod;
using Contract.DTOs.Response.ContentMod;

namespace Service.Interfaces
{
    public interface IMoodMusicService
    {
        Task<IReadOnlyList<MoodResponse>> GetMoodsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<MoodTrackResponse>> GetTracksAsync(string? moodCode, CancellationToken ct = default);
        Task<MoodTrackResponse> CreateAsync(Guid moderatorId, MoodTrackCreateRequest request, CancellationToken ct = default);
        Task<MoodTrackResponse> UpdateAsync(Guid trackId, MoodTrackUpdateRequest request, CancellationToken ct = default);
        Task DeleteAsync(Guid trackId, CancellationToken ct = default);
    }
}
