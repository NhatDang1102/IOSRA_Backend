using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Index("story_id", Name = "ix_chapter_story")]
[Index("story_id", "chapter_no", Name = "ux_chapter_story_no", IsUnique = true)]
public partial class chapter
{
    [Key]
    public ulong chapter_id { get; set; }

    public ulong story_id { get; set; }

    public uint chapter_no { get; set; }

    public uint dias_price { get; set; }

    [Column(TypeName = "enum('free','coin','sub_only')")]
    public string access_type { get; set; } = null!;

    [Column(TypeName = "enum('draft','published','locked','removed')")]
    public string status { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [InverseProperty("chapter")]
    public virtual ICollection<chapter_comment> chapter_comments { get; set; } = new List<chapter_comment>();

    [InverseProperty("chapter")]
    public virtual ICollection<chapter_localization> chapter_localizations { get; set; } = new List<chapter_localization>();

    [InverseProperty("chapter")]
    public virtual ICollection<chapter_purchase_log> chapter_purchase_logs { get; set; } = new List<chapter_purchase_log>();

    [InverseProperty("chapter")]
    public virtual ICollection<chapter_voice> chapter_voices { get; set; } = new List<chapter_voice>();

    [InverseProperty("chapter")]
    public virtual ICollection<content_approve> content_approves { get; set; } = new List<content_approve>();

    [ForeignKey("story_id")]
    [InverseProperty("chapters")]
    public virtual story story { get; set; } = null!;
}
