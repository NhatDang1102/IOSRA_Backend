using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class VerifyOtpRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 characters.")]
        public string Otp { get; set; } = null!;
    }
}
