using System;
using Contract.DTOs.Response.Common;

namespace Contract.DTOs.Response.Chapter
{
    public class StoryCommentFeedResponse
    {
        public Guid StoryId { get; set; }
        public Guid? ChapterFilterId { get; set; }
        public PagedResult<ChapterCommentResponse> Comments { get; set; } = null!;
    }
}
