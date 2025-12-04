using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Admin
{
    public class UpdateAccountStatusRequest
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}
