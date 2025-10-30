using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public record OpenAiImageResult(Stream Data, string FileName, string ContentType);

    public interface IOpenAiImageService
    {
        Task<OpenAiImageResult> GenerateCoverAsync(string prompt, CancellationToken ct = default);
    }
}
