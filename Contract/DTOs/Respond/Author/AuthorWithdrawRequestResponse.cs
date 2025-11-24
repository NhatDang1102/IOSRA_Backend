using System;

namespace Contract.DTOs.Respond.Author
{
    public class AuthorWithdrawRequestResponse
    {
        public Guid RequestId { get; set; }
        public long Amount { get; set; }
        public string Status { get; set; } = null!;
        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string AccountHolderName { get; set; } = null!;
        public string? Commitment { get; set; }
        public string? Note { get; set; }
        public string? ModeratorNote { get; set; }
        public string? ModeratorUsername { get; set; }
        public string? TransactionCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
