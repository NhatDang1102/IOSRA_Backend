using System;

namespace Contract.DTOs.Respond.OperationMod
{
    public class OpRequestItemResponse
    {
        public Guid RequestId { get; set; }
        public Guid RequesterId { get; set; }
        public string RequesterUsername { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? AssignedOmodId { get; set; }
    }
}
