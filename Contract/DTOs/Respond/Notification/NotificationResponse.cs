using System;

namespace Contract.DTOs.Respond.Notification
{
    public class NotificationResponse
    {
        public Guid NotificationId { get; set; }
        public Guid RecipientId { get; set; }
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public object? Payload { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
