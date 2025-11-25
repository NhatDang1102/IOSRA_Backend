using System;
using System.Collections.Generic;

namespace Contract.DTOs.Response.Story
{
    public class StoryResponse
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = null!;
        public bool IsPremium { get; set; }
        public string? CoverUrl { get; set; }
        public decimal? AiScore { get; set; }
        public string? AiResult { get; set; }
        public string? AiFeedback { get; set; }
        public string? ModeratorStatus { get; set; }
        public string? ModeratorNote { get; set; }
        public string Outline { get; set; } = null!;
        public string LengthPlan { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public IReadOnlyList<StoryTagResponse> Tags { get; set; } = Array.Empty<StoryTagResponse>();
    }
}
