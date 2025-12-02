using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("report")]
[Index("moderator_id", Name = "ix_reports_moderator")]
[Index("reporter_id", Name = "ix_reports_reporter")]
[Index("target_type", "target_id", Name = "ix_reports_target")]
public partial class report
{
    [Key]
    
    public Guid report_id { get; set; }

    [Column(TypeName = "enum('story','chapter','comment','user')")]
    public string target_type { get; set; } = null!;

    
    public Guid target_id { get; set; }

    
    public Guid reporter_id { get; set; }

    [Column(TypeName = "enum('negative_content','misinformation','spam','ip_infringement')")]
    public string reason { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? details { get; set; }

    [Column(TypeName = "enum('pending','resolved','rejected')")]
    public string status { get; set; } = null!;

    
    public Guid? moderator_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? reviewed_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("moderator_id")]
    [InverseProperty("reports")]
    public virtual ContentMod? moderator { get; set; }

    [ForeignKey("reporter_id")]
    [InverseProperty("reports")]
    public virtual account reporter { get; set; } = null!;
}
