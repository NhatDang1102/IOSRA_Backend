using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Author
{
    public class AuthorWithdrawRequest
    {
        [Range(1000, long.MaxValue, ErrorMessage = "Amount must be at least 1000 units.")]
        public long Amount { get; set; }

        [Required]
        [StringLength(100)]
        public string BankName { get; set; } = null!;

        [Required]
        [StringLength(64)]
        public string BankAccountNumber { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string AccountHolderName { get; set; } = null!;

        [StringLength(300)]
        public string? Commitment { get; set; }
    }
}
