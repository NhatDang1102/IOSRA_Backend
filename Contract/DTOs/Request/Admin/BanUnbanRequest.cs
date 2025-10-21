using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Admin
{
    /// <summary>Yêu cầu ban/unban (lý do tùy chọn để log)</summary>
    public class BanUnbanRequest
    {
        [MaxLength(200, ErrorMessage = "Reason tối đa 200 ký tự.")]
        public string? Reason { get; set; }
    }
}
