using System;

namespace Contract.DTOs.Respond.Chapter
{
    public class ChapterCommentResponse
    {
        public Guid CommentId { get; set; }
        public Guid StoryId { get; set; }
        public Guid ChapterId { get; set; }
        public int ChapterNo { get; set; }
        public string ChapterTitle { get; set; } = null!;
        public Guid ReaderId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string Content { get; set; } = null!;
        public bool IsLocked { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public string? ViewerReaction { get; set; }
    }
}
