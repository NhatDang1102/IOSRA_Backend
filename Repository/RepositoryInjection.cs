using Microsoft.Extensions.DependencyInjection;
using Repository.Interfaces;
using Repository.Repositories;

namespace Repository
{
    public static class RepositoryInjection
    {
        public static IServiceCollection AddRepositoryServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthRepository, AuthRepository>();
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IProfileRepository, ProfileRepository>();
            services.AddScoped<IOpRequestRepository, OpRequestRepository>();
            services.AddScoped<ITagRepository, TagRepository>();

            return services;
        }
    }
}

