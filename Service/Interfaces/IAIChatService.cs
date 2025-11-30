using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.AIChat;
using Contract.DTOs.Response.AIChat;

namespace Service.Interfaces
{
    public interface IAIChatService
    {
        Task<AiChatHistoryResponse> SendAsync(Guid accountId, AiChatSendRequest request, CancellationToken ct = default);

        Task<AiChatHistoryResponse> GetHistoryAsync(Guid accountId, CancellationToken ct = default);
    }
}
