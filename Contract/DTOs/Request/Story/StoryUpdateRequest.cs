using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Contract.DTOs.Request.Story
{
    public class StoryUpdateRequest
    {
        [StringLength(50, MinimumLength = 20, ErrorMessage = "Tiêu đề phải từ 20-50 kí tự.")]
        public string? Title { get; set; }

        public string? LanguageCode { get; set; }

        [StringLength(1000, MinimumLength = 6, ErrorMessage = "Mô tả phải từ 6-1000 kí tự.")]
        public string? Description { get; set; }

        [StringLength(1000, MinimumLength = 20, ErrorMessage = "Dàn ý phải từ 20-1000 kí tự.")]
        public string? Outline { get; set; }

        [RegularExpression("novel|short|super_short", ErrorMessage = "LengthPlan must be novel, short, or super_short.")]
        public string? LengthPlan { get; set; }

        public List<Guid>? TagIds { get; set; }

        [RegularExpression("upload", ErrorMessage = "CoverMode must be 'upload'.")]
        public string? CoverMode { get; set; }

        public IFormFile? CoverFile { get; set; }

        [StringLength(500)]
        public string? CoverPrompt { get; set; }
    }
}
