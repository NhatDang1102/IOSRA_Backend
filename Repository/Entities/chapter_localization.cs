using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_localizations")]
[PrimaryKey("chapter_id", "lang_id")]
[Index("lang_id", Name = "fk_chloc_lang")]
public partial class chapter_localization
{
    [Key]
    
    public Guid chapter_id { get; set; }

    [Key]
    
    public Guid lang_id { get; set; }

    public string content { get; set; } = null!;

    public uint word_count { get; set; }

    [StringLength(512)]
    public string? cloud_url { get; set; }

    [ForeignKey("chapter_id")]
    [InverseProperty("chapter_localizations")]
    public virtual chapter chapter { get; set; } = null!;

    [ForeignKey("lang_id")]
    [InverseProperty("chapter_localizations")]
    public virtual language_list lang { get; set; } = null!;
}
