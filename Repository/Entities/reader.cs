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
    public ulong account_id { get; set; }

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
    public virtual ICollection<favvorite_story> favvorite_stories { get; set; } = new List<favvorite_story>();

    [InverseProperty("follower")]
    public virtual ICollection<follow> follows { get; set; } = new List<follow>();
}
