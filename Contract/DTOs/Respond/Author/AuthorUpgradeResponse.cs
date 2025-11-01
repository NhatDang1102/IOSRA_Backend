using System;

namespace Contract.DTOs.Respond.Author
{
    public class AuthorUpgradeResponse
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = "pending";
        public Guid? AssignedOmodId { get; set; }
    }
}
