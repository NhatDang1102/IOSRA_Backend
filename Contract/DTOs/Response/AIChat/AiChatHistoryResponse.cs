using System.Collections.Generic;

namespace Contract.DTOs.Response.AIChat
{
    public class AiChatHistoryResponse
    {
        public IReadOnlyList<AiChatMessageDto> Messages { get; set; } = new List<AiChatMessageDto>();
    }
}
