using System;

namespace Contract.DTOs.Response.AIChat
{
    public class AiChatMessageDto
    {
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }
    }
}
