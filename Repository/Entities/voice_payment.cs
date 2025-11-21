using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_payment")]
[Index("wallet_id", Name = "ix_voice_payment_wallet")]
[Index("order_code", Name = "ux_voice_order_code", IsUnique = true)]
public partial class voice_payment
{
    [Key]
    public Guid topup_id { get; set; }

    public Guid wallet_id { get; set; }

    [StringLength(50)]
    public string provider { get; set; } = null!;

    [StringLength(50)]
    public string order_code { get; set; } = null!;

    [Column(TypeName = "bigint unsigned")]
    public ulong amount_vnd { get; set; }

    [Column(TypeName = "bigint unsigned")]
    public ulong chars_granted { get; set; }

    [Column(TypeName = "enum('pending','success','failed','refunded','cancelled')")]
    public string status { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("wallet_id")]
    [InverseProperty("voice_payments")]
    public virtual voice_wallet voice_wallet { get; set; } = null!;
}

