using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[PrimaryKey("story_id", "tag_id")]
[Index("tag_id", Name = "ix_story_tags_tag")]
public partial class story_tag
{
    [Key]
    public ulong story_id { get; set; }

    [Key]
    public uint tag_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("story_id")]
    [InverseProperty("story_tags")]
    public virtual story story { get; set; } = null!;

    [ForeignKey("tag_id")]
    [InverseProperty("story_tags")]
    public virtual tag tag { get; set; } = null!;
}
