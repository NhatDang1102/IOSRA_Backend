using System;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IVoiceAudioStorage
    {
        Task<string> UploadAsync(Guid storyId, Guid chapterId, Guid voiceId, byte[] data, CancellationToken ct = default);
        Task DeleteAsync(string key, CancellationToken ct = default);
        string GetPublicUrl(string key);
    }
}
