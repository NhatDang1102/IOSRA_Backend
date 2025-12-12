using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_price_rule")]
[Index(nameof(min_char_count), Name = "ix_voice_price_min")]
public partial class voice_price_rule
{
    [Key]
    public Guid rule_id { get; set; }

    public uint min_char_count { get; set; }

    public uint? max_char_count { get; set; }

    public uint dias_price { get; set; }

    public uint generation_dias { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }
}
