using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;
    }
}
