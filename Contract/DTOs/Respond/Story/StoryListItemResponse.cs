﻿using System;
using System.Collections.Generic;

namespace Contract.DTOs.Respond.Story
{
    public class StoryListItemResponse
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string Status { get; set; } = null!;
        public bool IsPremium { get; set; }
        public string? CoverUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public IReadOnlyList<StoryTagResponse> Tags { get; set; } = Array.Empty<StoryTagResponse>();
    }
}
