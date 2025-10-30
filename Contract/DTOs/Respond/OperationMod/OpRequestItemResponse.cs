using System;

namespace Contract.DTOs.Respond.OperationMod
{
    public class OpRequestItemResponse
    {
        public ulong RequestId { get; set; }
        public ulong RequesterId { get; set; }
        public string Status { get; set; } = null!;
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public ulong? AssignedOmodId { get; set; }
    }
}
