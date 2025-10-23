using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Profile
{
    public class AvatarUploadRequest
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
