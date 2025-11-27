using System;

namespace Contract.DTOs.Response.Story
{
    public class FavoriteStoryResponse
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = string.Empty;
        public bool NotiNewChapter { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
