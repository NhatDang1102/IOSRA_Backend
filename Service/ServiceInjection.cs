using Microsoft.Extensions.DependencyInjection;
using Service.Helpers;
using Service.Implementations;
using Service.Interfaces;

namespace Service
{
    public static class ServiceInjection
    {
        public static IServiceCollection AddServiceServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailSender, MailSender>();
            services.AddSingleton<IOtpStore, RedisOtpStore>();
            services.AddSingleton<IJwtBlacklistService, RedisJwtBlacklist>();
            services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAdminService, Service.Services.AdminService>();

            services.AddSingleton<IImageUploader, CloudinaryUploader>();
            services.AddScoped<IProfileService, ProfileService>();

            return services;
        }
    }
}
