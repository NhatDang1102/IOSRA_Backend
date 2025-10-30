using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterCreateRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(255, ErrorMessage = "Title must not exceed 255 characters.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Language code is required.")]
        [StringLength(8, ErrorMessage = "Language code must not exceed 8 characters.")]
        public string LanguageCode { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Summary must not exceed 500 characters.")]
        public string? Summary { get; set; }

        [Required(ErrorMessage = "Content is required.")]
        public string Content { get; set; } = null!;
    }
}
