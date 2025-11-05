using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Interfaces;
using Service.Helpers;
using Service.Interfaces;

namespace Service.Background
{
    public class StoryWeeklyViewSyncJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromHours(1);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StoryWeeklyViewSyncJob> _logger;

        public StoryWeeklyViewSyncJob(IServiceScopeFactory scopeFactory, ILogger<StoryWeeklyViewSyncJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Story weekly view sync job started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // ignore on shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to synchronize story weekly views.");
                }

                try
                {
                    await Task.Delay(LoopDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Story weekly view sync job stopped.");
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            var utcNow = DateTime.UtcNow;
            var localNow = utcNow + StoryViewTimeHelper.LocalOffset;

            if (localNow.DayOfWeek != DayOfWeek.Sunday || localNow.Hour < 23)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var tracker = scope.ServiceProvider.GetRequiredService<IStoryViewTracker>();
            var repository = scope.ServiceProvider.GetRequiredService<IStoryWeeklyViewRepository>();

            var weekStartUtc = tracker.GetCurrentWeekStartUtc();
            if (await repository.HasWeekSnapshotAsync(weekStartUtc, ct))
            {
                return;
            }

            var views = await tracker.GetWeeklyViewsAsync(weekStartUtc, ct);
            if (views.Count == 0)
            {
                _logger.LogInformation("No story views to archive for week {WeekStart}.", weekStartUtc);
                return;
            }

            await repository.UpsertWeeklyViewsAsync(weekStartUtc, views, ct);
            _logger.LogInformation("Archived {Count} story weekly view records for week {WeekStart}.", views.Count, weekStartUtc);
        }
    }
}

