using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request đăng ký tài khoản mới
    public class RegisterRequest
    {
        // Username: 6-20 ký tự, không chứa khoảng trắng
        [Required(ErrorMessage = "Username ko đc để trống.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Username phải từ 6-20 kí tự.")]
        [RegularExpression(@"^[a-zA-Z0-9]\S*$", ErrorMessage = "Username không được bắt đầu bằng ký tự đặc biệt và không chứa khoảng trắng.")]
        public string Username { get; set; } = null!;

        // Email: phải hợp lệ theo format email
        [Required(ErrorMessage = "Email ko để trống.")]
        [EmailAddress(ErrorMessage = "Email ko hợp lệ.")]
        public string Email { get; set; } = null!;

        // Password: 6-20 ký tự, phải có ít nhất 1 chữ và 1 số
        [Required(ErrorMessage = "Password ko để trống.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password phải từ 6-20 kí tự.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$", ErrorMessage = "Password phải có ít nhất 1 số 1 chữ.")]
        public string Password { get; set; } = null!;

        // Xác nhận password phải khớp với Password
        [Required(ErrorMessage = "Password xác nhận ko để trống.")]
        [Compare(nameof(Password), ErrorMessage = "Password xác nhận ko khớp.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
