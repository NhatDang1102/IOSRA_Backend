using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("topup_pricing")]
[Index("amount_vnd", Name = "ux_topup_amount", IsUnique = true)]
public partial class topup_pricing
{
    [Key]
    public Guid pricing_id { get; set; }

    [Column(TypeName = "bigint unsigned")]
    public ulong amount_vnd { get; set; }

    [Column(TypeName = "bigint unsigned")]
    public ulong diamond_granted { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public bool is_active { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }
}
