using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    // DTO cho request đăng nhập bằng Google
    public class GoogleLoginRequest
    {
        // Google ID Token từ Firebase Authentication
        [Required(ErrorMessage = "Google ID token is required.")]
        public string IdToken { get; set; } = null!;
    }
}
