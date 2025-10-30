using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public class StoryCreateRequest
    {
        [Required]
        [StringLength(150)]
        public string Title { get; set; } = null!;

        [StringLength(5000)]
        public string? Description { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one tag is required.")]
        public List<uint> TagIds { get; set; } = new();

        [Required]
        [RegularExpression("upload|generate", ErrorMessage = "CoverMode must be 'upload' or 'generate'.")]
        public string CoverMode { get; set; } = null!;

        public IFormFile? CoverFile { get; set; }

        [StringLength(500)]
        public string? CoverPrompt { get; set; }
    }
}
