using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterContentStorage
    {
        Task<string> UploadAsync(ulong storyId, ulong chapterId, string content, CancellationToken ct = default);
        Task<string> DownloadAsync(string key, CancellationToken ct = default);
        Task DeleteAsync(string key, CancellationToken ct = default);
        string GetContentUrl(string key);
    }
}
