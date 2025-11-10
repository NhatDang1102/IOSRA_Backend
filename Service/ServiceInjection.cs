using Amazon.S3;
using Contract.DTOs.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Implementations;
using Service.Services;
using Service.Interfaces;
using Service.Background;
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

            services.AddHttpClient<OpenAiService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<OpenAiSettings>>().Value;
                var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.openai.com/v1" : settings.BaseUrl.TrimEnd('/');
                client.BaseAddress = new Uri($"{baseUrl}/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddTransient<IOpenAiModerationService>(sp => sp.GetRequiredService<OpenAiService>());
            services.AddTransient<IOpenAiImageService>(sp => sp.GetRequiredService<OpenAiService>());
            services.AddSingleton<IStoryViewTracker, StoryViewTracker>();

            services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAdminService, Service.Services.AdminService>();
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
            services.AddScoped<IChapterModerationService, ChapterModerationService>();
            services.AddScoped<IStoryModerationService, StoryModerationService>();
            services.AddScoped<IStoryRatingService, StoryRatingService>();
            services.AddScoped<IChapterCommentService, ChapterCommentService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IFollowerNotificationService, FollowerNotificationService>();
            services.AddScoped<IChapterPricingService, ChapterPricingService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddHostedService<StoryWeeklyViewSyncJob>();

            return services;
        }
    }
}

