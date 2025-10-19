using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request
{
    public class LoginRequest
    {
//identifier là cả mail/username
        public string Identifier { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
