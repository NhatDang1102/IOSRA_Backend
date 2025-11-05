using System;

namespace Contract.DTOs.Respond.Story
{
    public class StoryWeeklyHighlightResponse
    {
        public StoryCatalogListItemResponse Story { get; set; } = null!;
        public ulong WeeklyViewCount { get; set; }
        public DateTime WeekStartUtc { get; set; }
    }
}

