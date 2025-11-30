using System;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IMoodMusicStorage
    {
        Task<string> UploadAsync(string moodCode, Guid trackId, byte[] data, CancellationToken ct = default);
        Task DeleteAsync(string key, CancellationToken ct = default);
        string GetPublicUrl(string key);
    }
}
