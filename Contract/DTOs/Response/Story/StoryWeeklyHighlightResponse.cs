using System;

namespace Contract.DTOs.Response.Story
{
    public class StoryWeeklyHighlightResponse
    {
        public StoryCatalogListItemResponse Story { get; set; } = null!;
        public ulong WeeklyViewCount { get; set; }
        public DateTime WeekStartUtc { get; set; }
    }
}

