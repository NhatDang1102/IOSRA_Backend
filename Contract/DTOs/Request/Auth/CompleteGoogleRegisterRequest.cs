using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth;

public class CompleteGoogleRegisterRequest
{
    [Required]
    public string IdToken { get; set; } = null!;

    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^\S+$", ErrorMessage = "Username không được chứa khoảng trắng")]
    public string Username { get; set; } = null!;

    [Required, StringLength(20, MinimumLength = 6)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,20}$",
        ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ và 1 số, dài 6–20 ký tự.")]
    public string Password { get; set; } = null!;

    [Compare(nameof(Password), ErrorMessage = "Xác nhận mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = null!;
}
