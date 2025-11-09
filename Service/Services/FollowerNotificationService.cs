using System;
using System.Threading;
using System.Threading.Tasks;
using Repository.Interfaces;
using Service.Constants;
using Service.Interfaces;

namespace Service.Services
{
    public class FollowerNotificationService : IFollowerNotificationService
    {
        private readonly IAuthorFollowRepository _authorFollowRepository;
        private readonly INotificationService _notificationService;

        public FollowerNotificationService(IAuthorFollowRepository authorFollowRepository, INotificationService notificationService)
        {
            _authorFollowRepository = authorFollowRepository;
            _notificationService = notificationService;
        }

        public async Task NotifyStoryPublishedAsync(Guid authorId, string authorName, Guid storyId, string storyTitle, CancellationToken ct = default)
        {
            var followers = await _authorFollowRepository.GetFollowerIdsForNotificationsAsync(authorId, ct);
            foreach (var followerId in followers)
            {
                await _notificationService.CreateAsync(new NotificationCreateModel(
                    followerId,
                    NotificationTypes.NewStory,
                    $"{authorName} vừa xuất bản truyện mới",
                    $"\"{storyTitle}\" đã được lên kệ. Nhấn để đọc ngay!",
                    new
                    {
                        authorId,
                        storyId
                    }), ct);
            }
        }

        public async Task NotifyChapterPublishedAsync(Guid authorId, string authorName, Guid storyId, string storyTitle, Guid chapterId, string chapterTitle, int chapterNo, CancellationToken ct = default)
        {
            var followers = await _authorFollowRepository.GetFollowerIdsForNotificationsAsync(authorId, ct);
            foreach (var followerId in followers)
            {
                await _notificationService.CreateAsync(new NotificationCreateModel(
                    followerId,
                    NotificationTypes.NewChapter,
                    $"{authorName} vừa đăng chương mới",
                    $"Chương {chapterNo}: \"{chapterTitle}\" của truyện \"{storyTitle}\" đã phát hành.",
                    new
                    {
                        authorId,
                        storyId,
                        chapterId,
                        chapterNo
                    }), ct);
            }
        }
    }
}
