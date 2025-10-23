using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Respond.Admin
{
    /// <summary>Thông tin tài khoản hiển thị cho trang quản trị</summary>
    public class AccountAdminResponse
    {
        public ulong AccountId { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Status { get; set; } = default!;
        public byte Strike { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
