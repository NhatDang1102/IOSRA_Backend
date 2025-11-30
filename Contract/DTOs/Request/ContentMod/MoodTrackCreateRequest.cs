using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.ContentMod
{
    public class MoodTrackCreateRequest
    {
        [Required]
        [StringLength(32)]
        public string MoodCode { get; set; } = string.Empty;

        [Required]
        [StringLength(128, ErrorMessage = "Title must be less than 128 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Prompt { get; set; } = string.Empty;
    }
}
