using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Admin
{
    /// <summary>Cập nhật role cho tài khoản theo role_code</summary>
    public class UpdateRolesRequest
    {
        [Required, MinLength(1, ErrorMessage = "Cần ít nhất 1 role_code.")]
        [MaxLength(20, ErrorMessage = "Tối đa 20 role_code/lần.")]
        public List<string> RoleCodes { get; set; } = new();
    }
}
