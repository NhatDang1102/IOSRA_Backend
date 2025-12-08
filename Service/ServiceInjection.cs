using Amazon.S3;
using Contract.DTOs.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Implementations;
using Service.Services;
using Service.Interfaces;
using Service.Background;
using Service.Queues;
using System;
using System.Net.Http.Headers;

namespace Service
{
    public static class ServiceInjection
    {
        public static IServiceCollection AddServiceServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailSender, MailSender>();
            services.AddSingleton<IOtpStore, RedisOtpStore>();
            services.AddSingleton<IJwtBlacklistService, RedisJwtBlacklist>();
            services.AddSingleton<IImageUploader, CloudinaryUploader>();
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<CloudflareR2Settings>>().Value;
                var config = new AmazonS3Config
                {
                    ServiceURL = settings.Endpoint,
                    ForcePathStyle = true
                };
                return new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey, config);
            });
            services.AddSingleton<IChapterContentStorage, CloudflareR2ChapterStorage>();
            services.AddSingleton<IVoiceAudioStorage, CloudflareR2VoiceStorage>();
            services.AddSingleton<IMoodMusicStorage, CloudflareR2MoodMusicStorage>();
            services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();
            services.AddSingleton<IVoiceSynthesisQueue, VoiceSynthesisQueue>();

            services.AddHttpClient<OpenAiService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<OpenAiSettings>>().Value;
                var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.openai.com/v1" : settings.BaseUrl.TrimEnd('/');
                client.BaseAddress = new Uri($"{baseUrl}/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddHttpClient<IElevenLabsClient, ElevenLabsClient>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<ElevenLabsSettings>>().Value;
                var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.elevenlabs.io" : settings.BaseUrl.TrimEnd('/');
                client.BaseAddress = new Uri($"{baseUrl}/");
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("xi-api-key", settings.ApiKey);
                }
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
            });
            services.AddTransient<IOpenAiModerationService>(sp => sp.GetRequiredService<OpenAiService>());
            services.AddTransient<IOpenAiImageService>(sp => sp.GetRequiredService<OpenAiService>());
            services.AddTransient<IOpenAiTranslationService>(sp => sp.GetRequiredService<OpenAiService>());
            services.AddTransient<IOpenAiChatService>(sp => sp.GetRequiredService<OpenAiService>());
            services.AddSingleton<IStoryViewTracker, StoryViewTracker>();

            services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAuthorUpgradeService, AuthorUpgradeService>();
            services.AddScoped<IOperationModService, OperationModService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IPublicProfileService, PublicProfileService>();
            services.AddScoped<IAuthorFollowService, AuthorFollowService>();
            services.AddScoped<IStoryHighlightService, StoryHighlightService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<IAuthorStoryService, AuthorStoryService>();
            services.AddScoped<IStoryCatalogService, StoryCatalogService>();
            services.AddScoped<IAuthorChapterService, AuthorChapterService>();
            services.AddScoped<IChapterCatalogService, ChapterCatalogService>();
            services.AddScoped<IChapterPurchaseService, ChapterPurchaseService>();
            services.AddScoped<IChapterModerationService, ChapterModerationService>();
            services.AddScoped<IStoryModerationService, StoryModerationService>();
            services.AddScoped<IStoryRatingService, StoryRatingService>();
            services.AddScoped<IChapterCommentService, ChapterCommentService>();
            services.AddScoped<IFavoriteStoryService, FavoriteStoryService>();
            services.AddScoped<IAuthorRevenueService, AuthorRevenueService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IFollowerNotificationService, FollowerNotificationService>();
            services.AddScoped<IChapterPricingService, ChapterPricingService>();
            services.AddScoped<IChapterTranslationService, ChapterTranslationService>();
            services.AddScoped<IVoicePricingService, VoicePricingService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IVoicePaymentService, VoicePaymentService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<IVoiceChapterService, VoiceChapterService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IContentModHandlingService, ContentModHandlingService>();
            services.AddScoped<IAuthorRankPromotionService, AuthorRankPromotionService>();
            services.AddScoped<IContentModStatService, ContentModStatService>();
            services.AddScoped<IOperationModStatService, OperationModStatService>();
            services.AddScoped<IPaymentHistoryService, PaymentHistoryService>();
            services.AddScoped<IMoodMusicService, MoodMusicService>();
            services.AddScoped<IAIChatService, AIChatService>();
            services.AddHostedService<StoryWeeklyViewSyncJob>();
            services.AddHostedService<SubscriptionReminderJob>();
            services.AddHostedService<VoiceSynthesisWorker>();

            return services;
        }
    }
}


