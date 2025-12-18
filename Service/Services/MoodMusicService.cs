using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.ContentMod;
using Contract.DTOs.Response.ContentMod;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class MoodMusicService : IMoodMusicService
    {
        private const int DefaultDurationSeconds = 30;
        private readonly IMoodMusicRepository _repository;
        private readonly IElevenLabsClient _elevenLabsClient;
        private readonly IMoodMusicStorage _storage;

        public MoodMusicService(
            IMoodMusicRepository repository,
            IElevenLabsClient elevenLabsClient,
            IMoodMusicStorage storage)
        {
            _repository = repository;
            _elevenLabsClient = elevenLabsClient;
            _storage = storage;
        }
        //lấy list mood trong db
        public async Task<IReadOnlyList<MoodResponse>> GetMoodsAsync(CancellationToken ct = default)
        {
            var moods = await _repository.GetMoodsAsync(ct);
            var tracks = await _repository.GetTracksAsync(null, ct);
            var counts = tracks.GroupBy(t => t.mood_code).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            return moods.Select(m => new MoodResponse
            {
                MoodCode = m.mood_code,
                MoodName = m.mood_name,
                Description = m.description,
                TrackCount = counts.TryGetValue(m.mood_code, out var count) ? count : 0
            }).ToArray();
        }

        public async Task<IReadOnlyList<MoodTrackResponse>> GetTracksAsync(string? moodCode, CancellationToken ct = default)
        {
            var tracks = await _repository.GetTracksAsync(moodCode, ct);
            return tracks.Select(Map).ToArray();
        }
        //tạo bản nhạc mới 
        public async Task<MoodTrackResponse> CreateAsync(Guid moderatorId, MoodTrackCreateRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("InvalidRequest", "Nội dung yêu cầu là bắt buộc.", 400);
            }

            var moodCode = request.MoodCode.Trim().ToLowerInvariant();
            var mood = await _repository.GetMoodAsync(moodCode, ct)
                       ?? throw new AppException("MoodNotFound", "Mã tâm trạng không được hỗ trợ.", 404);

            var prompt = request.Prompt.Trim();
            if (prompt.Length < 20)
            {
                throw new AppException("PromptTooShort", "Prompt phải có ít nhất 20 ký tự.", 400);
            }

            var audio = await _elevenLabsClient.ComposeMusicAsync(prompt, ct: ct);
            var trackId = Guid.NewGuid();
            var key = await _storage.UploadAsync(moodCode, trackId, audio, ct);
            var now = TimezoneConverter.VietnamNow;

            var entity = new chapter_mood_track
            {
                track_id = trackId,
                mood_code = mood.mood_code,
                title = string.IsNullOrWhiteSpace(request.Title) ? $"{mood.mood_name} ambience" : request.Title.Trim(),
                duration_seconds = DefaultDurationSeconds,
                storage_path = key,
                created_at = TimezoneConverter.VietnamNow,
                updated_at = now
            };

            _repository.AddTrack(entity);
            await _repository.SaveChangesAsync(ct);

            entity.mood_codeNavigation = mood;
            return Map(entity);
        }
        //đổi tên nhạc
        public async Task<MoodTrackResponse> UpdateAsync(Guid trackId, MoodTrackUpdateRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("InvalidRequest", "Nội dung yêu cầu là bắt buộc.", 400);
            }

            var track = await _repository.GetTrackAsync(trackId, ct)
                        ?? throw new AppException("TrackNotFound", "Không tìm thấy bản nhạc tâm trạng.", 404);

            if (request.Title != null)
            {
                var title = request.Title.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    throw new AppException("TitleRequired", "Title không thể trống.", 400);
                }
                track.title = title;
            }

            track.updated_at = TimezoneConverter.VietnamNow;
            await _repository.SaveChangesAsync(ct);

            track.mood_codeNavigation ??= await _repository.GetMoodAsync(track.mood_code, ct) ?? new chapter_mood
            {
                mood_code = track.mood_code,
                mood_name = track.mood_code
            };

            return Map(track);
        }
        //xóa nhạc 
        public async Task DeleteAsync(Guid trackId, CancellationToken ct = default)
        {
            var track = await _repository.GetTrackAsync(trackId, ct)
                        ?? throw new AppException("TrackNotFound", "Không tìm thấy bản nhạc tâm trạng.", 404);

            if (!string.IsNullOrWhiteSpace(track.storage_path))
            {
                await _storage.DeleteAsync(track.storage_path, ct);
            }

            _repository.RemoveTrack(track);
            await _repository.SaveChangesAsync(ct);
        }

        private MoodTrackResponse Map(chapter_mood_track track) => new MoodTrackResponse
        {
            TrackId = track.track_id,
            MoodCode = track.mood_code,
            MoodName = track.mood_codeNavigation?.mood_name ?? track.mood_code,
            Title = track.title,
            DurationSeconds = track.duration_seconds,
            PublicUrl = track.storage_path,
            CreatedAt = track.created_at
        };

   
    }
}