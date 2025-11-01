using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[PrimaryKey("reader_id", "story_id")]
[Table("favorite_story")]
[Index("story_id", Name = "ix_fav_story")]
public partial class favorite_story
{
    [Key]
    
    public Guid reader_id { get; set; }

    [Key]
    
    public Guid story_id { get; set; }

    [Required]
    public bool? noti_new_chapter { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("reader_id")]
    [InverseProperty("favorite_stories")]
    public virtual reader reader { get; set; } = null!;

    [ForeignKey("story_id")]
    [InverseProperty("favorite_stories")]
    public virtual story story { get; set; } = null!;
}
