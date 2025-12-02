using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_mood_track")]
[Index("mood_code", Name = "ix_mood_track_mood")]
public partial class chapter_mood_track
{
    [Key]
    public Guid track_id { get; set; }

    [StringLength(32)]
    public string mood_code { get; set; } = null!;

    [StringLength(128)]
    public string title { get; set; } = null!;

    public int duration_seconds { get; set; }

    [StringLength(512)]
    public string storage_path { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("mood_code")]
    [InverseProperty("chapter_mood_tracks")]
    public virtual chapter_mood mood_codeNavigation { get; set; } = null!;
}
