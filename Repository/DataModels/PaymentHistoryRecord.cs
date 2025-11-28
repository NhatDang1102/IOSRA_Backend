using System;

namespace Repository.DataModels
{
    public class PaymentHistoryRecord
    {
        public Guid PaymentId { get; set; }
        public string Type { get; set; } = null!;
        public string Provider { get; set; } = null!;
        public string OrderCode { get; set; } = null!;
        public ulong AmountVnd { get; set; }
        public ulong? GrantedValue { get; set; }
        public string? GrantedUnit { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? PlanCode { get; set; }
        public string? PlanName { get; set; }
    }
}
