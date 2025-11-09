using System;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Notification
{
    public class NotificationReadRequest
    {
        [Required]
        public Guid NotificationId { get; set; }
    }
}
