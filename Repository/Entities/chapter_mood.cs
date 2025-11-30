using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_mood")]
public partial class chapter_mood
{
    [Key]
    [StringLength(32)]
    public string mood_code { get; set; } = null!;

    [StringLength(64)]
    public string mood_name { get; set; } = null!;

    [StringLength(255)]
    public string? description { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [InverseProperty("mood")]
    public virtual ICollection<chapter> chapters { get; set; } = new List<chapter>();

    [InverseProperty("mood_codeNavigation")]
    public virtual ICollection<chapter_mood_track> chapter_mood_tracks { get; set; } = new List<chapter_mood_track>();
}
