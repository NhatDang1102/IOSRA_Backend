using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Auth
{
    public class LoginRequest
    {
        // identifier là cả mail/username
        [Required(ErrorMessage = "Vui lòng nhập email hoặc username")]
        [StringLength(255, ErrorMessage = "Identifier quá dài")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Identifier không được chứa khoảng trắng")]
        public string Identifier { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 20 ký tự")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$",
            ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ và 1 số")]
        public string Password { get; set; } = null!;
    }
}
