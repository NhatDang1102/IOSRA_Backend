using Microsoft.Extensions.DependencyInjection;
using Service.Helpers;
using Service.Implementations;
using Service.Interfaces;
using Service.Services;

namespace Service
{
    public static class ServiceInjection
    {
        public static IServiceCollection AddServiceServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailSender, MailSender>();
            services.AddSingleton<IOtpStore, RedisOtpStore>();
            services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAdminService, AdminService>();
            return services;
        }
    }
}