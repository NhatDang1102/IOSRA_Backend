using System;

namespace Contract.DTOs.Response.Follow
{
    public class AuthorFollowStatusResponse
    {
        public bool IsFollowing { get; set; }
        public bool NotificationsEnabled { get; set; }
        public DateTime? FollowedAt { get; set; }
    }
}
