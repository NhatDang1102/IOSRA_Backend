using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IElevenLabsClient
    {
        Task<byte[]> SynthesizeAsync(string voiceId, string text, CancellationToken ct = default);
    }
}
