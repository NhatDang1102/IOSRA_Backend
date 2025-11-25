using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterModerationDecisionRequest
    {
        [Required]
        public bool Approve { get; set; }

        [StringLength(2000, ErrorMessage = "Moderator feedback must not exceed 2000 characters.")]
        public string? ModeratorFeedback { get; set; }
    }
}
