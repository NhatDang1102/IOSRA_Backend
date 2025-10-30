using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth
{
    public class GoogleLoginRequest
    {
        [Required(ErrorMessage = "Google ID token is required.")]
        public string IdToken { get; set; } = null!;
    }
}
