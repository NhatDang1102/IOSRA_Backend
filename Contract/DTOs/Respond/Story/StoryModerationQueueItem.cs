using System;
using System.Collections.Generic;

namespace Contract.DTOs.Respond.Story
{
    public class StoryModerationQueueItem
    {
        public Guid ReviewId { get; set; }
        public Guid StoryId { get; set; }
        public Guid AuthorId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string AuthorUsername { get; set; } = null!;
        public string? CoverUrl { get; set; }
        public decimal? AiScore { get; set; }
        public string? AiResult { get; set; }
        public string Status { get; set; } = null!;
        public DateTime SubmittedAt { get; set; }
        public string? PendingNote { get; set; }
        public IReadOnlyList<StoryTagResponse> Tags { get; set; } = Array.Empty<StoryTagResponse>();
    }
}
