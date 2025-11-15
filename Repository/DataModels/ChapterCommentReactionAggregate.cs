using System;

namespace Repository.DataModels
{
    public class ChapterCommentReactionAggregate
    {
        public Guid CommentId { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public string? ViewerReaction { get; set; }
    }
}
