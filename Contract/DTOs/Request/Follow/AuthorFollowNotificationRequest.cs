using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Follow
{
    public class AuthorFollowNotificationRequest
    {
        [Required]
        public bool EnableNotifications { get; set; }
    }
}
