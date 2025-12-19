using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Entities;

[Table("story_weekly_view")]
public partial class story_weekly_view
{
    [Key]
    public Guid story_weekly_view_id { get; set; }

    public Guid story_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime week_start_utc { get; set; }

    public ulong view_count { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime captured_at { get; set; }

    [ForeignKey(nameof(story_id))]
    public virtual story story { get; set; } = null!;
}

