using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = null!;
}
