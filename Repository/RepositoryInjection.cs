using Microsoft.Extensions.DependencyInjection;
using Repository.Interfaces;
using Repository.Repositories;

namespace Repository
{
    public static class RepositoryInjection
    {
        public static IServiceCollection AddRepositoryServices(this IServiceCollection services)
        {
            services.AddSingleton<ISnowflakeIdGenerator, YitterSnowflakeIdGenerator>();
            services.AddScoped<IAuthRepository, AuthRepository>();
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IProfileRepository, ProfileRepository>();
            services.AddScoped<IOpRequestRepository, OpRequestRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<IStoryRepository, StoryRepository>();
            services.AddScoped<IChapterRepository, ChapterRepository>();

            return services;
        }
    }
}
