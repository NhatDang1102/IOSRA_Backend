using System;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public class StoryCreateRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 20, ErrorMessage = "Tiêu đề phải có ít nhất 20 kí tự.")]
        public string Title { get; set; } = null!;

        [StringLength(1000, MinimumLength = 6, ErrorMessage = "Mô tả phải có ít nhất 20 kí tự.")]
        public string? Description { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 thể loại.")]
        public List<Guid> TagIds { get; set; } = new();

        [Required]
        [RegularExpression("upload|generate", ErrorMessage = "CoverMode must be 'upload' or 'generate'.")]
        public string CoverMode { get; set; } = null!;

        [Required]
        [StringLength(1000, MinimumLength = 20, ErrorMessage = "Dàn ý phải có ít nhất 20 kí tự.")]
        public string Outline { get; set; } = null!;

        [Required]
        [RegularExpression("novel|short|super_short", ErrorMessage = "LengthPlan must be novel, short, or super_short.")]
        public string LengthPlan { get; set; } = "short";

        public IFormFile? CoverFile { get; set; }

        [StringLength(500)]
        public string? CoverPrompt { get; set; }
    }
}
