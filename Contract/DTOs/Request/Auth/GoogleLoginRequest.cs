using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Auth;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = null!;
}
