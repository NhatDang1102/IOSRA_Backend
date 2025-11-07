using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request quên mật khẩu - gửi OTP
    public class ForgotPasswordRequest
    {
        // Email tài khoản cần reset password
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;
    }
}
