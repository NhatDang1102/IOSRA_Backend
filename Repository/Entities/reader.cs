using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("reader")]
public partial class reader
{
    [Key]
    
    public Guid account_id { get; set; }

    [Column(TypeName = "text")]
    public string? bio { get; set; }

    [Column(TypeName = "enum('male','female','other','unspecified')")]
    public string gender { get; set; } = null!;

    public DateOnly? birthdate { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("reader")]
    public virtual account account { get; set; } = null!;

    [InverseProperty("reader")]
    public virtual ICollection<chapter_comment> chapter_comments { get; set; } = new List<chapter_comment>();

    [InverseProperty("reader")]
    public virtual ICollection<favorite_story> favorite_stories { get; set; } = new List<favorite_story>();

    [InverseProperty("reader")]
    public virtual ICollection<story_rating> story_ratings { get; set; } = new List<story_rating>();

    [InverseProperty("reader")]
    public virtual ICollection<chapter_comment_reaction> chapter_comment_reactions { get; set; } = new List<chapter_comment_reaction>();

    [InverseProperty("follower")]
    public virtual ICollection<follow> follows { get; set; } = new List<follow>();
}
