using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("story")]
[Index("author_id", Name = "ix_story_author")]
public partial class story
{
    [Key]
    
    public Guid story_id { get; set; }

    public string title { get; set; } = null!;

    
    public Guid author_id { get; set; }

    [Column(TypeName = "mediumtext")]
    public string? desc { get; set; }

    [StringLength(512)]
    public string? cover_url { get; set; }

    [Column(TypeName = "enum('draft','pending','rejected','published','completed','hidden','removed')")]
    public string status { get; set; } = null!;

    public bool is_premium { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? published_at { get; set; }

    [ForeignKey("author_id")]
    [InverseProperty("stories")]
    public virtual author author { get; set; } = null!;

    [InverseProperty("story")]
    public virtual ICollection<chapter> chapters { get; set; } = new List<chapter>();

    [InverseProperty("story")]
    public virtual ICollection<content_approve> content_approves { get; set; } = new List<content_approve>();

    [InverseProperty("story")]
    public virtual ICollection<favorite_story> favorite_stories { get; set; } = new List<favorite_story>();

    [InverseProperty("story")]
    public virtual ICollection<story_tag> story_tags { get; set; } = new List<story_tag>();

    [InverseProperty("story")]
    public virtual ICollection<story_weekly_view> story_weekly_views { get; set; } = new List<story_weekly_view>();
}
