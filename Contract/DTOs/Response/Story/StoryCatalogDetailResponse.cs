using System;
using System.Collections.Generic;

namespace Contract.DTOs.Response.Story
{
    public class StoryCatalogDetailResponse
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = null!;
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = null!;
        public string? CoverUrl { get; set; }
        public bool IsPremium { get; set; }
        public string? Description { get; set; }
        public int TotalChapters { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string LengthPlan { get; set; } = null!;
        public IReadOnlyList<StoryTagResponse> Tags { get; set; } = Array.Empty<StoryTagResponse>();
    }
}
