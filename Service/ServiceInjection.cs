using Amazon.S3;
using Contract.DTOs.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Implementations;
using Service.Services;
using Service.Interfaces;
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

            services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAdminService, Service.Services.AdminService>();
            services.AddScoped<IAuthorUpgradeService, AuthorUpgradeService>();
            services.AddScoped<IOperationModService, OperationModService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<IStoryService, StoryService>();
            services.AddScoped<IChapterService, ChapterService>();
            services.AddScoped<IChapterModerationService, ChapterModerationService>();
            services.AddScoped<IStoryModerationService, StoryModerationService>();

            return services;
        }
    }
}

