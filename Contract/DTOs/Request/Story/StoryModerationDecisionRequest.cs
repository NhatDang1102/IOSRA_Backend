using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public class StoryModerationDecisionRequest
    {
        public bool Approve { get; set; }

        [StringLength(1000)]
        public string? ModeratorNote { get; set; }
    }
}
