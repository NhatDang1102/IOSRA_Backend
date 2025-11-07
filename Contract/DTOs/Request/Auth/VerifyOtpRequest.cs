using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request xác thực OTP đăng ký
    public class VerifyOtpRequest
    {
        // Email đã nhận OTP
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;

        // Mã OTP 6 chữ số
        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 characters.")]
        public string Otp { get; set; } = null!;
    }
}
