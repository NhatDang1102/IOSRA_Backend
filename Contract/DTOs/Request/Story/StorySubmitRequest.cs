using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public class StorySubmitRequest
    {
        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
