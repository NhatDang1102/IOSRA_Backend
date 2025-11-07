using System;
using Contract.DTOs.Respond.Common;

namespace Contract.DTOs.Respond.Chapter
{
    public class StoryCommentFeedResponse
    {
        public Guid StoryId { get; set; }
        public Guid? ChapterFilterId { get; set; }
        public PagedResult<ChapterCommentResponse> Comments { get; set; } = null!;
    }
}
