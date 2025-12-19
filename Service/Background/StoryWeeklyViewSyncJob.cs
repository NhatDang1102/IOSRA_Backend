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
            //đọc bảng story weekly view
            var weeklyViewRepo = scope.ServiceProvider.GetRequiredService<IStoryWeeklyViewRepository>();
            var storyRepo = scope.ServiceProvider.GetRequiredService<IStoryCatalogRepository>();

            //xác định tuần hiện tại 
            var weekStartUtc = tracker.GetCurrentWeekStartUtc();
            //xác định số view mới 
            var newViews = await tracker.GetWeeklyViewsAsync(weekStartUtc, ct);
            //nếu ko có view mới ->  end luôn task
            if (newViews.Count == 0)
            {
                _logger.LogDebug("No new story views to process for week {WeekStart}.", weekStartUtc);
                return;
            }
            //gom hết id của story mới
            var storyIds = newViews.Select(v => v.StoryId).ToArray();
            //lấy số lượt xem từ đợt update cuối cùng
            var lastViews = await weeklyViewRepo.GetWeeklyViewsByStoryIdsAsync(weekStartUtc, storyIds, ct);
            //convert result từ db -> dictionary thay vì duyệt từng mảng
            var lastViewsLookup = lastViews.ToDictionary(v => v.StoryId, v => v.ViewCount);

            var viewIncrements = new Dictionary<Guid, ulong>();
            //duyệt qua từng story có trong data mới từ redis
            foreach (var view in newViews)
            {
                //lấy ra số view từ lần lưu gần nhất trong db, ko có thì tính là 0
                lastViewsLookup.TryGetValue(view.StoryId, out var lastCount);
                //nếu có chênh lệch thì lấy lần mới - lần cũ ra số view mới
                if (view.ViewCount > lastCount)
                {
                    viewIncrements[view.StoryId] = view.ViewCount - lastCount;
                }
            }
            //nếu story có lượt view mới thì tiếp tục
            if (viewIncrements.Count > 0)
            {
                //cộng dồn số view mới tính ở trên vô db
                await storyRepo.IncrementTotalViewsAsync(viewIncrements, ct);
                _logger.LogInformation("Incremented total views for {Count} stories.", viewIncrements.Count);
            }
            //cập nhật bảng story weekly view để cho highlight story, và làm cơ sở cho lần bắt đầu tiếp theo
            await weeklyViewRepo.UpsertWeeklyViewsAsync(weekStartUtc, newViews, ct);
            _logger.LogInformation("Upserted {Count} story weekly view records for week {WeekStart}.", newViews.Count, weekStartUtc);
        }
    }
}
