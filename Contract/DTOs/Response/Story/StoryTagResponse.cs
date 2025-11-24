using System;

namespace Contract.DTOs.Response.Story
{
    public class StoryTagResponse
    {
        public Guid TagId { get; set; }
        public string TagName { get; set; } = null!;
    }
}
