using System;
using System.Collections.Generic;

namespace Contract.DTOs.Response.OperationMod
{
    // 1. User Growth
    public class UserGrowthStatsResponse
    {
        public List<UserGrowthPoint> Data { get; set; } = new();
    }

    public class UserGrowthPoint
    {
        public DateTime Date { get; set; }
        public int NewReaders { get; set; }
        public int NewAuthors { get; set; }
        public int TotalNew { get; set; }
    }

    // 2. Trending Stories
    public class TrendingStoryResponse
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public long TotalViewsInPeriod { get; set; }
    }

    // 3. System Engagement
    public class SystemEngagementResponse
    {
        public long TotalViews { get; set; }
        public int TotalNewComments { get; set; }
        public int TotalNewFollows { get; set; }
        public List<EngagementPoint> ChartData { get; set; } = new();
    }

    public class EngagementPoint
    {
        public DateTime Date { get; set; }
        public int NewComments { get; set; }
        public int NewFollows { get; set; }
    }

    // 4. Tag Trends
    public class TagTrendResponse
    {
        public Guid TagId { get; set; }
        public string TagName { get; set; } = string.Empty;
        public long TotalViews { get; set; }
        public int StoryCount { get; set; }
    }
}
