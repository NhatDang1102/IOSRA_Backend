using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request hoàn tất đăng ký tài khoản Google (tạo username + password)
    public class CompleteGoogleRegisterRequest
    {
        // Google ID Token để xác thực
        [Required(ErrorMessage = "Google ID token is required.")]
        public string IdToken { get; set; } = null!;

        // Username mong muốn (3-50 ký tự, không chứa khoảng trắng)
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Username cannot contain whitespace.")]
        public string Username { get; set; } = null!;

        // Password: 6-20 ký tự, phải có ít nhất 1 chữ và 1 số
        [Required(ErrorMessage = "Password is required.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,20}$", ErrorMessage = "Password must contain at least one letter and one number.")]
        public string Password { get; set; } = null!;

        // Xác nhận password
        [Compare(nameof(Password), ErrorMessage = "Password confirmation does not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
