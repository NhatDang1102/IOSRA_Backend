using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterCreateRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 10, ErrorMessage = "Tiêu đề phải từ 10-50 kí tự.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Không được để trống nội dung chương.")]
        public string Content { get; set; } = null!;

        [RegularExpression("^(?i)(free|dias)$", ErrorMessage = "Chỉ được chọn 1 trong 2 loại miễn phí/trả phí")]
        public string? AccessType { get; set; }
    }
}
