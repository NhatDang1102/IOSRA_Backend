using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Profile
{
    public class ChangeEmailRequest
    {
        [Required, EmailAddress]
        public string NewEmail { get; set; } = null!;
    }
}
