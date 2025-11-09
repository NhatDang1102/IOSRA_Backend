using System;

namespace Contract.DTOs.Internal
{
    public class AuthorFollowerProjection
    {
        public Guid FollowerId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool NotificationsEnabled { get; set; }
        public DateTime FollowedAt { get; set; }
    }
}
