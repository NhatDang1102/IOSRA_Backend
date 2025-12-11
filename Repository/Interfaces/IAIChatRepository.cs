using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.DataModels;

namespace Repository.Interfaces
{
    public interface IAIChatRepository
    {
        Task<IReadOnlyList<AiChatStoredMessage>> GetHistoryAsync(Guid accountId, CancellationToken ct = default);

        Task AppendAsync(Guid accountId, IReadOnlyList<AiChatStoredMessage> messages, CancellationToken ct = default);

        Task TrimAsync(Guid accountId, int maxMessages, CancellationToken ct = default);

        Task<IReadOnlyList<string>> SearchContentAsync(IEnumerable<string> keywords, int limit = 5, CancellationToken ct = default);
    }
}
