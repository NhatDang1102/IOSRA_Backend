using System;
using System.Collections.Generic;

namespace Contract.DTOs.Respond.Story
{
    public class StoryModerationQueueItem
    {
        public ulong StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ulong AuthorId { get; set; }
        public string AuthorUsername { get; set; } = null!;
        public string? CoverUrl { get; set; }
        public decimal? AiScore { get; set; }
        public string? AiResult { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? PendingNote { get; set; }
        public IReadOnlyCollection<StoryTagResponse> Tags { get; set; } = Array.Empty<StoryTagResponse>();
    }
}
