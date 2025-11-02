using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("content_approve")]
[Index("moderator_id", Name = "fk_cappr_moderator")]
[Index("chapter_id", Name = "ix_cappr_chapter")]
[Index("story_id", Name = "ix_cappr_story")]
public partial class content_approve
{
    [Key]
    
    public Guid review_id { get; set; }

    [Column(TypeName = "enum('story','chapter')")]
    public string approve_type { get; set; } = null!;

    
    public Guid? story_id { get; set; }

    
    public Guid? chapter_id { get; set; }

    [Precision(5, 2)]
    public decimal? ai_score { get; set; }

    [Column(TypeName = "text")]
    public string? ai_note { get; set; }

    [Column(TypeName = "enum('pending','approved','rejected')")]
    public string status { get; set; } = null!;

    
    public Guid? moderator_id { get; set; }

    [Column(TypeName = "text")]
    public string? moderator_note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("chapter_id")]
    [InverseProperty("content_approves")]
    public virtual chapter? chapter { get; set; }

    [ForeignKey("moderator_id")]
    [InverseProperty("content_approves")]
    public virtual ContentMod? moderator { get; set; }

    [ForeignKey("story_id")]
    [InverseProperty("content_approves")]
    public virtual story? story { get; set; }
}
