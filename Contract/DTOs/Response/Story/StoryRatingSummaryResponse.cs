using System;
using System.Collections.Generic;
using Contract.DTOs.Response.Common;

namespace Contract.DTOs.Response.Story
{
    public class StoryRatingSummaryResponse
    {
        public Guid StoryId { get; set; }
        public decimal? AverageScore { get; set; }
        public int TotalRatings { get; set; }
        public IReadOnlyDictionary<byte, int> Distribution { get; set; } = new Dictionary<byte, int>();
        public StoryRatingItemResponse? ViewerRating { get; set; }
        public PagedResult<StoryRatingItemResponse> Ratings { get; set; } = null!;
    }
}
