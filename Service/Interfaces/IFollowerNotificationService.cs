using System;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IFollowerNotificationService
    {
        Task NotifyStoryPublishedAsync(Guid authorId, string authorName, Guid storyId, string storyTitle, CancellationToken ct = default);
        Task NotifyChapterPublishedAsync(Guid authorId, string authorName, Guid storyId, string storyTitle, Guid chapterId, string chapterTitle, int chapterNo, CancellationToken ct = default);
    }
}
