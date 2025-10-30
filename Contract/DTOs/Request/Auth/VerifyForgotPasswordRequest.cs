using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class VerifyForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 characters.")]
        public string Otp { get; set; } = null!;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,20}$", ErrorMessage = "Password must contain at least one letter and one number.")]
        public string NewPassword { get; set; } = null!;

        [Compare(nameof(NewPassword), ErrorMessage = "Password confirmation does not match.")]
        public string ConfirmNewPassword { get; set; } = null!;
    }
}
