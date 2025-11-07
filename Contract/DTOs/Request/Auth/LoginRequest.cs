using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request đăng nhập
    public class LoginRequest
    {
        // Identifier có thể là email hoặc username
        [Required(ErrorMessage = "Please provide an email address or username.")]
        [StringLength(255, ErrorMessage = "Identifier is too long.")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Identifier cannot contain whitespace.")]
        public string Identifier { get; set; } = null!;

        // Password: 6-20 ký tự, phải có ít nhất 1 chữ và 1 số
        [Required(ErrorMessage = "Please provide a password.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$", ErrorMessage = "Password must contain at least one letter and one number.")]
        public string Password { get; set; } = null!;
    }
}
