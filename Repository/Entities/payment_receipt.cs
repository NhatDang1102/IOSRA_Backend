using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("payment_receipt")]
[Index("account_id", Name = "ix_receipt_account")]
public partial class payment_receipt
{
    [Key]
    public Guid receipt_id { get; set; }

    public Guid account_id { get; set; }

    public Guid ref_id { get; set; }

    [Column(TypeName = "enum('dia_topup','voice_topup','subscription')")]
    public string type { get; set; } = null!;

    [Column(TypeName = "bigint unsigned")]
    public ulong amount_vnd { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("payment_receipts")]
    public virtual account account { get; set; } = null!;
}
