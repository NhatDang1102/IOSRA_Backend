using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Username không được bỏ trống")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Username phải từ 3 đến 20 ký tự")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Username không được chứa khoảng trắng")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Email không được bỏ trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được bỏ trống")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 20 ký tự")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$",
            ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ và 1 số")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Xác nhận mật khẩu không được bỏ trống")]
        [Compare(nameof(Password), ErrorMessage = "Xác nhận mật khẩu không khớp")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
