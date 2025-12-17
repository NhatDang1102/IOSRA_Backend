using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public record ModerationViolation(
        string Word,
        int Count,
        IReadOnlyList<string> Samples);

    public record OpenAiModerationResult(
        bool ShouldReject,
        double Score,
        IReadOnlyList<ModerationViolation> Violations,
        string Content,
        string SanitizedContent,
        string Explanation);

    public interface IOpenAiModerationService
    {
        Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, string outline, CancellationToken ct = default);
        Task<OpenAiModerationResult> ModerateChapterAsync(string title, string content, CancellationToken ct = default);
        Task<string> SummarizeChapterAsync(string content, CancellationToken ct = default);
        Task<string> DetectMoodAsync(string content, CancellationToken ct = default);
    }
}
