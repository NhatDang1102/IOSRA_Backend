using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Profile
{
    public class VerifyChangeEmailRequest
    {
        [Required, StringLength(6, MinimumLength = 6)]
        public string Otp { get; set; } = null!;
    }
}
