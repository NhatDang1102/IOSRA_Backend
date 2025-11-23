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

        [Required(ErrorMessage = "Content is required.")]
        public string Content { get; set; } = null!;

        [RegularExpression("^(?i)(free|dias)$", ErrorMessage = "AccessType must be either 'free' or 'dias'.")]
        public string? AccessType { get; set; }
    }
}
