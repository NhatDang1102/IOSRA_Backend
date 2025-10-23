using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Auth
{
    public class VerifyForgotPasswordRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required, StringLength(6, MinimumLength = 6)]
        public string Otp { get; set; } = null!;

        [Required, StringLength(20, MinimumLength = 6)]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,20}$",
            ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ và 1 số, dài 6–20 ký tự.")]
        public string NewPassword { get; set; } = null!;

        [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp")]
        public string ConfirmNewPassword { get; set; } = null!;
    }
}