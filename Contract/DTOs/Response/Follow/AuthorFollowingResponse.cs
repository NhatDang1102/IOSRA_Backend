using System;

namespace Contract.DTOs.Response.Follow
{
    public class AuthorFollowingResponse
    {
        public Guid AuthorId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool NotificationsEnabled { get; set; }
        public DateTime FollowedAt { get; set; }
    }
}
