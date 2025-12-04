using System;
using System.Collections.Generic;

namespace Repository.DataModels
{
    public class AdminAccountProjection
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public byte Strike { get; set; }
        public string StrikeStatus { get; set; } = string.Empty;
        public DateTime? StrikeRestrictedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }
}
