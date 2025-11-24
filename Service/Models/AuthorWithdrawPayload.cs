namespace Service.Models
{
    internal sealed class AuthorWithdrawPayload
    {
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string AccountHolderName { get; set; } = string.Empty;
        public string? Commitment { get; set; }
    }
}
