using System;

namespace Contract.DTOs.Response.Story
{
    public class StoryRatingItemResponse
    {
        public Guid ReaderId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public byte Score { get; set; }
        public DateTime RatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
