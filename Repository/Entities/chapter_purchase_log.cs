using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_purchase_log")]
[Index("account_id", Name = "ix_purchase_account")]
[Index("chapter_id", "account_id", Name = "ux_purchase_unique", IsUnique = true)]
public partial class chapter_purchase_log
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid chapter_purchase_id { get; set; }

    [Column(TypeName = "char(36)")]
    public Guid chapter_id { get; set; }

    [Column(TypeName = "char(36)")]
    public Guid account_id { get; set; }

    public uint dia_price { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("chapter_purchase_logs")]
    public virtual account account { get; set; } = null!;

    [ForeignKey("chapter_id")]
    [InverseProperty("chapter_purchase_logs")]
    public virtual chapter chapter { get; set; } = null!;
}
