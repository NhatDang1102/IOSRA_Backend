using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("author_rank_upgrade_request")]
[Index("author_id", Name = "ix_rank_upgrade_author")]
[Index("status", Name = "ix_rank_upgrade_status")]
public class author_rank_upgrade_request
{
    [Key]
    public Guid request_id { get; set; }

    public Guid author_id { get; set; }

    public Guid? current_rank_id { get; set; }

    public Guid target_rank_id { get; set; }

    [Required]
    [StringLength(100)]
    public string full_name { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "text")]
    public string commitment { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "enum('pending','approved','rejected')")]
    public string status { get; set; } = "pending";

    public Guid? omod_id { get; set; }

    [Column(TypeName = "text")]
    public string? mod_note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? reviewed_at { get; set; }

    [ForeignKey(nameof(author_id))]
    public virtual author author { get; set; } = null!;

    [ForeignKey(nameof(target_rank_id))]
    public virtual author_rank target_rank { get; set; } = null!;

    [ForeignKey(nameof(omod_id))]
    public virtual account? moderator { get; set; }
}
