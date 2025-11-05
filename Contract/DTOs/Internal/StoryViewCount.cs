using System;

namespace Contract.DTOs.Internal
{
    public class StoryViewCount
    {
        public Guid StoryId { get; set; }
        public ulong ViewCount { get; set; }
    }
}

