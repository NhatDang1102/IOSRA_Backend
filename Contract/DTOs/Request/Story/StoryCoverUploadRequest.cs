using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Contract.DTOs.Request.Story
{
    public class StoryCoverUploadRequest
    {
        [Required]
        public IFormFile CoverFile { get; set; } = null!;
    }
}
