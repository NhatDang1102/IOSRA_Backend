using System;
using System.Collections.Generic;

namespace Contract.DTOs.Respond.Story
{
    public class StoryResponse
    {
        public ulong StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = null!;
        public bool IsPremium { get; set; }
        public string? CoverUrl { get; set; }
        public decimal? AiScore { get; set; }
        public string? AiResult { get; set; }
        public string? ModeratorStatus { get; set; }
        public string? ModeratorNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IReadOnlyCollection<StoryTagResponse> Tags { get; set; } = Array.Empty<StoryTagResponse>();
    }
}
