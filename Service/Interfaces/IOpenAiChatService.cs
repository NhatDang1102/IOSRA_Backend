using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IOpenAiChatService
    {
        Task<string> ChatAsync(IReadOnlyList<AiChatPromptMessage> messages, CancellationToken ct = default);
    }

    public record AiChatPromptMessage(string Role, string Content);
}
