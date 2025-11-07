using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_comment")]
[Index("chapter_id", Name = "ix_cmt_chapter")]
[Index("reader_id", Name = "ix_cmt_reader")]
public partial class chapter_comment
{
    [Key]
    
    public Guid comment_id { get; set; }

    
    public Guid reader_id { get; set; }

    
    public Guid story_id { get; set; }

    
    public Guid chapter_id { get; set; }

    [Column(TypeName = "text")]
    public string content { get; set; } = null!;

    [Column(TypeName = "enum('visible','hidden','removed')")]
    public string status { get; set; } = null!;

    [Column(TypeName = "bit(1)")]
    public bool is_locked { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("chapter_id")]
    [InverseProperty("chapter_comments")]
    public virtual chapter chapter { get; set; } = null!;

    [ForeignKey("reader_id")]
    [InverseProperty("chapter_comments")]
    public virtual reader reader { get; set; } = null!;

    [ForeignKey("story_id")]
    [InverseProperty("chapter_comments")]
    public virtual story story { get; set; } = null!;
}
