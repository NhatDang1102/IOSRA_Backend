using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_list")]
[Index("voice_name", Name = "ux_voice_name", IsUnique = true)]
[Index("voice_code", Name = "ux_voice_code", IsUnique = true)]
public partial class voice_list
{
    [Key]
    public Guid voice_id { get; set; }

    [StringLength(64)]
    public string voice_name { get; set; } = null!;

    [StringLength(32)]
    public string voice_code { get; set; } = null!;

    [StringLength(128)]
    public string provider_voice_id { get; set; } = null!;

    [StringLength(256)]
    public string? description { get; set; }

    public bool is_active { get; set; } = true;

    [InverseProperty("voice")]
    public virtual ICollection<chapter_voice> chapter_voices { get; set; } = new List<chapter_voice>();

    [InverseProperty("voice")]
    public virtual ICollection<voice_purchase_item> voice_purchase_items { get; set; } = new List<voice_purchase_item>();
}
