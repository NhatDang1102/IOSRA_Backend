using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.AIChat;
using Contract.DTOs.Response.AIChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class AIChatController : AppControllerBase
    {
        private readonly IAIChatService _chatService;

        public AIChatController(IAIChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet("history")]
        public Task<AiChatHistoryResponse> GetHistory(CancellationToken ct)
            => _chatService.GetHistoryAsync(AccountId, ct);

        [HttpPost("message")]
        public Task<AiChatHistoryResponse> SendMessage([FromBody] AiChatSendRequest request, CancellationToken ct)
            => _chatService.SendAsync(AccountId, request, ct);
    }
}
