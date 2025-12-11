using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Interfaces;
using Service.Interfaces;

namespace Service.Background
{
    public class SummaryBackfillJob : BackgroundService
    {
        private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SummaryBackfillJob> _logger;

        public SummaryBackfillJob(IServiceScopeFactory scopeFactory, ILogger<SummaryBackfillJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Summary backfill job started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                bool processedAny = false;
                try
                {
                    processedAny = await RunBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Summary backfill job encountered an error.");
                }

                // If we processed items, rest briefly to respect rate limits.
                // If we found nothing, sleep longer.
                var delay = processedAny ? ProcessingDelay : IdleDelay;
                
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Summary backfill job stopped.");
        }

        private async Task<bool> RunBatchAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var chapterRepo = scope.ServiceProvider.GetRequiredService<IAuthorChapterRepository>();
            var contentStorage = scope.ServiceProvider.GetRequiredService<IChapterContentStorage>();
            var openAiService = scope.ServiceProvider.GetRequiredService<IOpenAiModerationService>();

            // Fetch a small batch of chapters missing summary
            var chapters = await chapterRepo.GetChaptersMissingSummaryAsync(5, ct);
            if (chapters.Count == 0)
            {
                return false;
            }

            foreach (var chap in chapters)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (string.IsNullOrWhiteSpace(chap.content_url)) continue;

                    var content = await contentStorage.DownloadAsync(chap.content_url, ct);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var summary = await openAiService.SummarizeChapterAsync(content, ct);
                        
                        // Only update if we got a valid summary to avoid loops on failed generations
                        if (!string.IsNullOrWhiteSpace(summary))
                        {
                            chap.summary = summary;
                            await chapterRepo.UpdateAsync(chap, ct);
                            _logger.LogInformation("Generated summary for chapter {ChapterId}", chap.chapter_id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate summary for chapter {ChapterId}", chap.chapter_id);
                }
            }

            return true;
        }
    }
}
