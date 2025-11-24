using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Profile
{
    public class ProfileResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? AvatarUrl { get; set; }

        public string? Bio { get; set; }
        public string Gender { get; set; } = "unspecified";
        public DateOnly? Birthday { get; set; }
        public byte Strike { get; set; }
        public string StrikeStatus { get; set; } = "none";
        public DateTime? StrikeRestrictedUntil { get; set; }
        public long VoiceCharBalance { get; set; }
        public bool IsAuthor { get; set; }
        public AuthorProfileSummary? Author { get; set; }
    }

    public class AuthorProfileSummary
    {
        public Guid AuthorId { get; set; }
        public bool IsRestricted { get; set; }
        public bool IsVerified { get; set; }
        public uint TotalFollower { get; set; }
        public uint TotalStory { get; set; }
        public Guid? RankId { get; set; }
        public string? RankName { get; set; }
        public decimal? RankRewardRate { get; set; }
        public uint? RankMinFollowers { get; set; }
    }
}
