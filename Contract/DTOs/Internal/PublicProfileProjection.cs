using System;

namespace Contract.DTOs.Internal
{
    public class PublicProfileProjection
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? Bio { get; set; }
        public string? Gender { get; set; }

        public bool IsAuthor { get; set; }
        public bool AuthorRestricted { get; set; }
        public bool AuthorVerified { get; set; }
        public string? AuthorRankName { get; set; }
        public decimal? AuthorRankRewardRate { get; set; }
        public uint? AuthorRankMinFollowers { get; set; }
        public int FollowerCount { get; set; }
        public int PublishedStoryCount { get; set; }
        public DateTime? LatestPublishedAt { get; set; }
    }
}

