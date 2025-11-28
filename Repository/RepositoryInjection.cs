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
            services.AddScoped<IPublicProfileRepository, PublicProfileRepository>();
            services.AddScoped<IOpRequestRepository, OpRequestRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<IAuthorStoryRepository, AuthorStoryRepository>();
            services.AddScoped<IStoryCatalogRepository, StoryCatalogRepository>();
            services.AddScoped<IStoryModerationRepository, StoryModerationRepository>();
            services.AddScoped<IAuthorChapterRepository, AuthorChapterRepository>();
            services.AddScoped<IChapterCatalogRepository, ChapterCatalogRepository>();
            services.AddScoped<IChapterModerationRepository, ChapterModerationRepository>();
            services.AddScoped<IStoryWeeklyViewRepository, StoryWeeklyViewRepository>();
            services.AddScoped<IStoryRatingRepository, StoryRatingRepository>();
            services.AddScoped<IChapterCommentRepository, ChapterCommentRepository>();
            services.AddScoped<IAuthorFollowRepository, AuthorFollowRepository>();
            services.AddScoped<IFavoriteStoryRepository, FavoriteStoryRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IChapterPricingRepository, ChapterPricingRepository>();
            services.AddScoped<IVoicePricingRepository, VoicePricingRepository>();
            services.AddScoped<IReportRepository, ReportRepository>();
            services.AddScoped<IModerationRepository, ModerationRepository>();
            services.AddScoped<IBillingRepository, BillingRepository>();
            services.AddScoped<IChapterPurchaseRepository, ChapterPurchaseRepository>();
            services.AddScoped<IAuthorRevenueRepository, AuthorRevenueRepository>();
            services.AddScoped<IPaymentHistoryRepository, PaymentHistoryRepository>();
            services.AddScoped<IContentModStatRepository, ContentModStatRepository>();
            services.AddScoped<IOperationModStatRepository, OperationModStatRepository>();

            return services;
        }
    }
}
