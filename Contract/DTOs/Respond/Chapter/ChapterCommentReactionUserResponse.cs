using System;

namespace Contract.DTOs.Respond.Chapter
{
    public class ChapterCommentReactionUserResponse
    {
        public Guid ReaderId { get; set; }
        public string Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string ReactionType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
