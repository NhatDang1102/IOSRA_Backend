using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Admin
{
    public class CreateModeratorRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(32)]
        public string? Phone { get; set; }
    }
}
