using System;
using System.Collections.Generic;

namespace Contract.DTOs.Response.Admin
{
    /// <summary>Account summary for the admin panel.</summary>
    public class AccountAdminResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Status { get; set; } = default!;
        public byte Strike { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
