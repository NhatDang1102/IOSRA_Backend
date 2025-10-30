using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class CompleteGoogleRegisterRequest
    {
        [Required(ErrorMessage = "Google ID token is required.")]
        public string IdToken { get; set; } = null!;

        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Username cannot contain whitespace.")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,20}$", ErrorMessage = "Password must contain at least one letter and one number.")]
        public string Password { get; set; } = null!;

        [Compare(nameof(Password), ErrorMessage = "Password confirmation does not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
