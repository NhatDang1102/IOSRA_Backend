using System;

namespace Contract.DTOs.Response.Author
{
    public class AuthorUpgradeResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = "pending";
        public Guid? AssignedOmodId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ModeratorFeedback { get; set; }
    }
}
