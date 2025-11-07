using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterCommentUpdateRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Comment must be between 1 and 2000 characters.")]
        public string Content { get; set; } = null!;
    }
}
