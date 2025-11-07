using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request xác thực OTP và đặt lại mật khẩu mới
    public class VerifyForgotPasswordRequest
    {
        // Email tài khoản
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;

        // Mã OTP 6 chữ số đã nhận qua email
        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 characters.")]
        public string Otp { get; set; } = null!;

        // Mật khẩu mới: 6-20 ký tự, phải có ít nhất 1 chữ và 1 số
        [Required(ErrorMessage = "New password is required.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,20}$", ErrorMessage = "Password must contain at least one letter and one number.")]
        public string NewPassword { get; set; } = null!;

        // Xác nhận mật khẩu mới
        [Compare(nameof(NewPassword), ErrorMessage = "Password confirmation does not match.")]
        public string ConfirmNewPassword { get; set; } = null!;
    }
}
