using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request đăng nhập
    public class LoginRequest
    {
        // Identifier có thể là email hoặc username
        [Required(ErrorMessage = "Email/Username không được để trống.")]
        [StringLength(255, ErrorMessage = "Email/username quá dài.")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Email/Username không được có space.")]
        public string Identifier { get; set; } = null!;

        // Password: 6-20 ký tự, phải có ít nhất 1 chữ và 1 số
        [Required(ErrorMessage = "Password không được để trống.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password phải từ 6-20 kí tự.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$", ErrorMessage = "Password phải có 1 chữ 1 số, không space.")]
        public string Password { get; set; } = null!;
    }
}
