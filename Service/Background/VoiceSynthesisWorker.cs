using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.DBContext;
using Repository.Entities;
using Repository.Utils;
using Service.Interfaces;
using Service.Models;

namespace Service.Background
{
    // Background Service xử lý việc tạo giọng đọc AI (Text-to-Speech)
    // Flow: 
    // 1. Lấy tin nhắn từ hàng đợi (Queue).
    // 2. Tải nội dung văn bản của chương truyện.
    // 3. Chia nhỏ văn bản (Chunking) vì API ElevenLabs giới hạn ký tự mỗi lần gọi.
    // 4. Gọi ElevenLabs API để chuyển text thành audio cho từng đoạn.
    // 5. Ghép các đoạn audio lại thành file hoàn chỉnh.
    // 6. Upload file audio lên Cloud (R2) và cập nhật trạng thái "Ready" vào DB.
    public class VoiceSynthesisWorker : BackgroundService
    {
        private const int MaxChunkCharacters = 4800; // Giới hạn ký tự an toàn cho 1 lần gọi API

        private readonly IVoiceSynthesisQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VoiceSynthesisWorker> _logger;

        public VoiceSynthesisWorker(
            IVoiceSynthesisQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<VoiceSynthesisWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Voice synthesis worker started.");

            // Lắng nghe hàng đợi liên tục
            await foreach (var job in _queue.DequeueAsync(stoppingToken))
            {
                try
                {
                    await ProcessJobAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Đang tắt service
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Voice synthesis job crashed for chapter {ChapterId} voice {VoiceId}.", job.ChapterId, job.VoiceId);
                }
            }

            _logger.LogInformation("Voice synthesis worker stopped.");
        }

        private async Task ProcessJobAsync(VoiceSynthesisJob job, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var contentStorage = scope.ServiceProvider.GetRequiredService<IChapterContentStorage>();
            var voiceStorage = scope.ServiceProvider.GetRequiredService<IVoiceAudioStorage>();
            var elevenLabs = scope.ServiceProvider.GetRequiredService<IElevenLabsClient>();

            // 1. Tìm bản ghi yêu cầu tạo voice trong DB
            var row = await db.chapter_voice
                .Include(cv => cv.voice)
                .Include(cv => cv.chapter)
                    .ThenInclude(c => c.story)
                .FirstOrDefaultAsync(cv => cv.chapter_id == job.ChapterId && cv.voice_id == job.VoiceId, ct);

            if (row == null)
            {
                _logger.LogWarning("Voice job skipped because chapter_voice row was not found for chapter {ChapterId} voice {VoiceId}.", job.ChapterId, job.VoiceId);
                return;
            }

            // 2. Kiểm tra tính hợp lệ của dữ liệu
            if (row.chapter == null)
            {
                row.status = "failed";
                row.error_message = "Chapter navigation not loaded.";
                row.completed_at = TimezoneConverter.VietnamNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.chapter.content_url))
            {
                row.status = "failed";
                row.error_message = "Chapter content is missing from storage.";
                row.completed_at = TimezoneConverter.VietnamNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            var providerVoiceId = row.voice?.provider_voice_id;
            if (string.IsNullOrWhiteSpace(providerVoiceId))
            {
                row.status = "failed";
                row.error_message = "Voice preset is missing provider voice id.";
                row.completed_at = TimezoneConverter.VietnamNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            try
            {
                // Cập nhật trạng thái đang xử lý
                row.status = "processing";
                row.error_message = null;
                row.completed_at = null;
                await db.SaveChangesAsync(ct);

                // 3. Tải text từ Cloud Storage
                var content = (await contentStorage.DownloadAsync(row.chapter.content_url, ct)).Trim();
                if (content.Length == 0)
                {
                    throw new InvalidOperationException("Chapter content is empty.");
                }

                // 4. Chia nhỏ văn bản thành các đoạn (Chunks) dựa trên dấu cách/xuống dòng để tránh cắt ngang từ
                var chunks = SplitContentIntoChunks(content, MaxChunkCharacters);
                var audioParts = new List<byte[]>(chunks.Count);
                
                // 5. Gọi ElevenLabs API cho từng đoạn
                foreach (var chunk in chunks)
                {
                    var audio = await elevenLabs.SynthesizeAsync(providerVoiceId, chunk, ct);
                    audioParts.Add(audio);
                }

                // 6. Ghép các đoạn mp3 lại thành 1 file duy nhất
                var merged = MergeAudioChunks(audioParts);
                
                // 7. Upload file kết quả lên Cloud (Cloudflare R2)
                var storageKey = await voiceStorage.UploadAsync(row.chapter.story_id, row.chapter_id, row.voice_id, merged, ct);

                // 8. Hoàn tất: Lưu URL file và đổi trạng thái sang Ready
                row.storage_path = storageKey;
                row.status = "ready";
                row.completed_at = TimezoneConverter.VietnamNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Xử lý khi có lỗi (Hết tiền API, lỗi mạng,...)
                row.status = "failed";
                row.error_message = ex.Message;
                row.completed_at = TimezoneConverter.VietnamNow;
                await db.SaveChangesAsync(ct);
                _logger.LogError(ex, "Voice synthesis failed for chapter {ChapterId} voice {VoiceId}.", job.ChapterId, job.VoiceId);
            }
        }

        private static IReadOnlyList<string> SplitContentIntoChunks(string content, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Length <= maxCharacters)
            {
                return new[] { content };
            }

            var chunks = new List<string>();
            var pointer = 0;
            var total = content.Length;

            while (pointer < total)
            {
                var remaining = total - pointer;
                var take = Math.Min(maxCharacters, remaining);
                var end = pointer + take;

                if (end < total)
                {
                    var breakIndex = content.LastIndexOfAny(new[] { '\n', '\r', ' ', '\t' }, end - 1, take);
                    if (breakIndex > pointer)
                    {
                        end = breakIndex + 1;
                    }
                }

                var chunk = content.Substring(pointer, end - pointer).Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }

                pointer = end;
            }

            return chunks.Count == 0 ? new[] { content } : chunks;
        }

        private static byte[] MergeAudioChunks(IReadOnlyList<byte[]> parts)
        {
            if (parts.Count == 1)
            {
                return parts[0];
            }

            using var stream = new MemoryStream();
            foreach (var segment in parts.Where(p => p is { Length: > 0 }))
            {
                stream.Write(segment, 0, segment.Length);
            }

            return stream.ToArray();
        }
    }
}
