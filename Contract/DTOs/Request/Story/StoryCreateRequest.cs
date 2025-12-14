using System;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public class StoryCreateRequest
    {
        [Required]
        [MinLength(20)]
        [StringLength(150)]
        public string Title { get; set; } = null!;

        [StringLength(5000)]
        [MinLength(20)]
        public string? Description { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one tag is required.")]
        public List<Guid> TagIds { get; set; } = new();

        [Required]
        [RegularExpression("upload|generate", ErrorMessage = "CoverMode must be 'upload' or 'generate'.")]
        public string CoverMode { get; set; } = null!;

        [Required]
        [MinLength(20)]
        [StringLength(10000)]
        public string Outline { get; set; } = null!;

        [Required]
        [RegularExpression("novel|short|super_short", ErrorMessage = "LengthPlan must be novel, short, or super_short.")]
        public string LengthPlan { get; set; } = "short";

        public IFormFile? CoverFile { get; set; }

        [StringLength(500)]
        public string? CoverPrompt { get; set; }
    }
}
