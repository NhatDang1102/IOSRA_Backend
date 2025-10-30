using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 20 characters.")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Username cannot contain whitespace.")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email is invalid.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$", ErrorMessage = "Password must contain at least one letter and one number.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Password confirmation is required.")]
        [Compare(nameof(Password), ErrorMessage = "Password confirmation does not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
