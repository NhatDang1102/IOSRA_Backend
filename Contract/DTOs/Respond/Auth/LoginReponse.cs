using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Respond.Auth
{
    // DTO response cho đăng nhập thành công (cũng dùng cho verify registration)
    public class LoginResponse
    {
        public Guid AccountId { get; set; } // ID tài khoản
        public string Username { get; set; } = null!; // Tên người dùng
        public string Email { get; set; } = null!; // Email
        public string Token { get; set; } = null!; // JWT token
        public List<string> Roles { get; set; } = new(); // Danh sách roles của user

    }
}
