using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterUpdateRequest
    {
        [StringLength(50, MinimumLength = 10, ErrorMessage = "Tiêu đề phải từ 10-50 kí tự.")]
        public string? Title { get; set; }
        public string? Content { get; set; }
        [RegularExpression("^(?i)(free|dias)$", ErrorMessage = "AccessType must be either 'free' or 'dias'.")]
        public string? AccessType { get; set; }
    }
}
