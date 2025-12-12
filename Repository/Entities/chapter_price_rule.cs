using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Entities;

[Table("chapter_price_rule")]
public class chapter_price_rule
{
    [Key]
    
    public Guid rule_id { get; set; }

    public uint min_char_count { get; set; }

    public uint? max_char_count { get; set; }

    public uint dias_price { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }
}
