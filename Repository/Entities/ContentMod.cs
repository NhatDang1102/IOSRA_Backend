using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("ContentMod")]
public partial class ContentMod
{
    [Key]
    
    public Guid account_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime assigned_date { get; set; }

    [StringLength(32)]
    public string? phone { get; set; }

    public uint total_approved_stories { get; set; }

    public uint total_rejected_stories { get; set; }

    public uint total_reported_handled { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("ContentMod")]
    public virtual account account { get; set; } = null!;

    [InverseProperty("moderator")]
    public virtual ICollection<content_approve> content_approves { get; set; } = new List<content_approve>();

    [InverseProperty("moderator")]
    public virtual ICollection<report> reports { get; set; } = new List<report>();
}
