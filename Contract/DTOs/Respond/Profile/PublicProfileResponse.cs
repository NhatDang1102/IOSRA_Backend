using System;

namespace Contract.DTOs.Respond.Profile
{
    public class PublicProfileResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        public string? Gender { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsAuthor { get; set; }
        public AuthorPublicProfileResponse? Author { get; set; }
    }

    public class AuthorPublicProfileResponse
    {
        public Guid AuthorId { get; set; }
        public string? RankName { get; set; }
        public bool IsRestricted { get; set; }
        public bool IsVerified { get; set; }
        public int FollowerCount { get; set; }
        public int PublishedStoryCount { get; set; }
        public DateTime? LatestPublishedAt { get; set; }
    }
}

