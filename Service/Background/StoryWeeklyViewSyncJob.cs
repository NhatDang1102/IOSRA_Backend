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
    //inherit từ backgroundservice của .net core để cho phép job này chạy liên tục và độc lập trên nền sv
    public class StoryWeeklyViewSyncJob : BackgroundService
    {
        //định coi mấy tiếng background này sẽ lặp lại (loop liên tục mỗi 6 tiếng)
        private static readonly TimeSpan LoopDelay = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StoryWeeklyViewSyncJob> _logger;

        public StoryWeeklyViewSyncJob(IServiceScopeFactory scopeFactory, ILogger<StoryWeeklyViewSyncJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }
        //định nghĩa loop liên tục
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Story weekly view sync job started.");
            //trừ khi tắt sv hoặc có req đặc biệt
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
                 //delay theo time set ở trên
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
            //tạo depedency injection scoped mới (mỗi req http) để mỗi lần using var scope xong thì giải phóng
            using var scope = _scopeFactory.CreateScope();
            //lấy service track lượt view (redis)
            var tracker = scope.ServiceProvider.GetRequiredService<IStoryViewTracker>();
            //lấy repo để truy vấn db
            var repository = scope.ServiceProvider.GetRequiredService<IStoryWeeklyViewRepository>();

            var weekStartUtc = tracker.GetCurrentWeekStartUtc();

            //bóc TẤT CẢ lượt xem tuần này trên redis
            var views = await tracker.GetWeeklyViewsAsync(weekStartUtc, ct);
            if (views.Count == 0)
            {
                _logger.LogDebug("No story views to archive for week {WeekStart}.", weekStartUtc);
                return;
            }
            //upsert vào db
            await repository.UpsertWeeklyViewsAsync(weekStartUtc, views, ct);
            _logger.LogInformation("Upserted {Count} story weekly view records for week {WeekStart}.", views.Count, weekStartUtc);
        }
    }
}
