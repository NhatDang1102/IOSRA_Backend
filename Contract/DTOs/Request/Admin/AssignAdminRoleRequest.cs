using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Admin
{
    public class AssignAdminRoleRequest
    {
        [Required]
        public string Role { get; set; } = string.Empty;
    }
}
