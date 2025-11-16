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

    
    public Guid? parent_comment_id { get; set; }

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

    [InverseProperty("comment")]
    public virtual ICollection<chapter_comment_reaction> chapter_comment_reactions { get; set; } = new List<chapter_comment_reaction>();

    [ForeignKey("parent_comment_id")]
    [InverseProperty("replies")]
    public virtual chapter_comment? parent_comment { get; set; }

    [InverseProperty("parent_comment")]
    public virtual ICollection<chapter_comment> replies { get; set; } = new List<chapter_comment>();
}
