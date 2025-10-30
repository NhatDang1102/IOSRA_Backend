using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public record OpenAiModerationResult(bool IsFlagged, double? Score, string?[] Categories);

    public interface IOpenAiModerationService
    {
        Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, CancellationToken ct = default);
    }
}
