using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_voices")]
[PrimaryKey("chapter_id", "voice_id")]
[Index("voice_id", Name = "fk_chvoice_voice")]
public partial class chapter_voice
{
    [Key]
    public Guid chapter_id { get; set; }

    [Key]
    public Guid voice_id { get; set; }

    [StringLength(512)]
    public string? cloud_url { get; set; }

    [StringLength(512)]
    public string? storage_path { get; set; }

    [StringLength(16)]
    [Column(TypeName = "enum('pending','processing','ready','failed')")]
    public string status { get; set; } = "pending";

    [Column(TypeName = "datetime")]
    public DateTime requested_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? completed_at { get; set; }

    public int char_cost { get; set; }

    public uint dias_price { get; set; }

    [Column(TypeName = "text")]
    public string? error_message { get; set; }

    [ForeignKey("chapter_id")]
    [InverseProperty("chapter_voices")]
    public virtual chapter chapter { get; set; } = null!;

    [ForeignKey("voice_id")]
    [InverseProperty("chapter_voices")]
    public virtual voice_list voice { get; set; } = null!;
}
