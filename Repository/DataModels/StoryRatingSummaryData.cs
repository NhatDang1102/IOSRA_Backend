using System;
using System.Collections.Generic;

namespace Repository.DataModels
{
    public class StoryRatingSummaryData
    {
        public Guid StoryId { get; }
        public decimal? AverageScore { get; }
        public int TotalRatings { get; }
        public IReadOnlyDictionary<byte, int> Distribution { get; }

        public StoryRatingSummaryData(Guid storyId, decimal? averageScore, int totalRatings, IReadOnlyDictionary<byte, int> distribution)
        {
            StoryId = storyId;
            AverageScore = averageScore;
            TotalRatings = totalRatings;
            Distribution = distribution;
        }
    }
}
