using System;

namespace Contract.DTOs.Response.Author
{
    public class AuthorRevenueTransactionItemResponse
    {
        public Guid TransactionId { get; set; }
        public string Type { get; set; } = null!;
        public long AmountVnd { get; set; }
        public Guid? PurchaseLogId { get; set; }
        public Guid? RequestId { get; set; }
        public object? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
