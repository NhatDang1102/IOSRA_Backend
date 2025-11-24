using System;

namespace Contract.DTOs.Response.Follow
{
    public class AuthorFollowerResponse
    {
        public Guid FollowerId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool NotificationsEnabled { get; set; }
        public DateTime FollowedAt { get; set; }
    }
}
