using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Respond.Profile
{
    public class ProfileResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? AvatarUrl { get; set; }

        public string? Bio { get; set; }
        public string Gender { get; set; } = "unspecified";
        public DateOnly? Birthday { get; set; }
    }
}
