using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public class StoryRatingRequest
    {
        [Range(1, 5, ErrorMessage = "Score must be between 1 and 5.")]
        public byte Score { get; set; }
    }
}
