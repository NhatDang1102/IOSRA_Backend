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

    [ForeignKey("chapter_id")]
    [InverseProperty("chapter_voices")]
    public virtual chapter chapter { get; set; } = null!;

    [ForeignKey("voice_id")]
    [InverseProperty("chapter_voices")]
    public virtual voice_list voice { get; set; } = null!;
}
