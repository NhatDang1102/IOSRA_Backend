using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_purchase_item")]
[Index(nameof(account_id), nameof(chapter_id), nameof(voice_id), Name = "ux_voice_purchase_item", IsUnique = true)]
[Index(nameof(voice_purchase_id), Name = "ix_voice_purchase_item_purchase")]
[Index(nameof(voice_id), Name = "ix_voice_purchase_item_voice")]
public partial class voice_purchase_item
{
    [Key]
    public Guid purchase_item_id { get; set; }

    public Guid voice_purchase_id { get; set; }

    public Guid account_id { get; set; }

    public Guid chapter_id { get; set; }

    public Guid voice_id { get; set; }

    public uint dia_price { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey(nameof(account_id))]
    [InverseProperty(nameof(account.voice_purchase_items))]
    public virtual account account { get; set; } = null!;

    [ForeignKey(nameof(chapter_id))]
    [InverseProperty(nameof(chapter.voice_purchase_items))]
    public virtual chapter chapter { get; set; } = null!;

    [ForeignKey(nameof(voice_purchase_id))]
    [InverseProperty(nameof(voice_purchase_log.voice_purchase_items))]
    public virtual voice_purchase_log purchase { get; set; } = null!;

    [ForeignKey(nameof(voice_id))]
    [InverseProperty(nameof(voice_list.voice_purchase_items))]
    public virtual voice_list voice { get; set; } = null!;
}
