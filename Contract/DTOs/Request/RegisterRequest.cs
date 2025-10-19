using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Username không được bỏ trống")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Username phải từ 3 đến 20 ký tự")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Email không được bỏ trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được bỏ trống")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 20 ký tự")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{6,20}$",
            ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ và 1 số")]
        public string Password { get; set; } = null!;
    }

}
