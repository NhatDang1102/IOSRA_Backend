using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Entities;

[Table("voice_wallet_payment")]
public partial class voice_wallet_payment
{
    [Key]
    public Guid trs_id { get; set; }

    public Guid wallet_id { get; set; }

    [StringLength(32)]
    [Column(TypeName = "enum('topup','purchase','refund')")]
    public string type { get; set; } = "purchase";

    public long char_delta { get; set; }

    public long char_after { get; set; }

    public Guid? ref_id { get; set; }

    [StringLength(255)]
    public string? note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("wallet_id")]
    [InverseProperty("voice_wallet_payments")]
    public virtual voice_wallet voice_wallet { get; set; } = null!;
}
