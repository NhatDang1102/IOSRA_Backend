using System;

namespace Repository.DataModels
{
    public class AiChatStoredMessage
    {
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public static AiChatStoredMessage Create(string role, string content, DateTime timestamp)
            => new AiChatStoredMessage
            {
                Role = role,
                Content = content,
                Timestamp = timestamp
            };
    }
}
