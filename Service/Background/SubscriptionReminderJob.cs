using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.DBContext;
using Repository.Utils;
using Service.Constants;
using Service.Interfaces;

namespace Service.Background
{
    public class SubscriptionReminderJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromHours(3);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionReminderJob> _logger;

        public SubscriptionReminderJob(IServiceScopeFactory scopeFactory, ILogger<SubscriptionReminderJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Subscription reminder job started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // ignored during shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Subscription reminder job failed.");
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

            _logger.LogInformation("Subscription reminder job stopped.");
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = TimezoneConverter.VietnamNow;
            var today = DateOnly.FromDateTime(now);

            var subscriptions = await db.subcriptions
                .AsNoTracking()
                .Include(s => s.plan_codeNavigation)
                .Where(s => s.start_at <= now && s.end_at >= now)
                .Where(s => !s.last_claim_date.HasValue || s.last_claim_date.Value != today)
                .ToListAsync(ct);

            if (subscriptions.Count == 0)
            {
                _logger.LogDebug("No subscription reminders needed at {Timestamp}.", now);
                return;
            }

            var latestByUser = subscriptions
                .GroupBy(s => s.user_id)
                .Select(g => g.OrderByDescending(x => x.end_at).First())
                .ToList();

            foreach (var subscription in latestByUser)
            {
                var plan = subscription.plan_codeNavigation;
                var dailyDias = plan?.daily_dias ?? 0u;
                var planName = plan?.plan_name ?? subscription.plan_code;

                try
                {
                    await notificationService.CreateAsync(new NotificationCreateModel(
                        subscription.user_id,
                        NotificationTypes.SubscriptionReminder,
                        "Đừng quên nhận dias hôm nay",
                        $"Bạn vẫn chưa nhận {dailyDias} dias từ gói {planName}. Nhấn để nhận ngay!",
                        new
                        {
                            planCode = subscription.plan_code,
                            planName,
                            dailyDias
                        }), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send subscription reminder to {UserId}.", subscription.user_id);
                }
            }

            _logger.LogInformation("Sent {Count} subscription reminders at {Timestamp}.", latestByUser.Count, now);
        }
    }
}
