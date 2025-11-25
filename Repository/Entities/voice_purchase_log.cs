using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_purchase_log")]
[Index(nameof(account_id), Name = "ix_voice_purchase_account")]
[Index(nameof(chapter_id), Name = "ix_voice_purchase_chapter")]
public partial class voice_purchase_log
{
    [Key]
    public Guid voice_purchase_id { get; set; }

    public Guid chapter_id { get; set; }

    public Guid account_id { get; set; }

    public uint total_dias { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey(nameof(account_id))]
    [InverseProperty("voice_purchase_logs")]
    public virtual account account { get; set; } = null!;

    [ForeignKey(nameof(chapter_id))]
    [InverseProperty("voice_purchase_logs")]
    public virtual chapter chapter { get; set; } = null!;

    [InverseProperty(nameof(voice_purchase_item.purchase))]
    public virtual ICollection<voice_purchase_item> voice_purchase_items { get; set; } = new List<voice_purchase_item>();

    [InverseProperty(nameof(author_revenue_transaction.voice_purchase))]
    public virtual ICollection<author_revenue_transaction> author_revenue_transactions { get; set; } = new List<author_revenue_transaction>();
}
