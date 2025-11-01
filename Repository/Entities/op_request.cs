using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Index(nameof(requester_id), Name = "ix_opreq_requester")]
[Index(nameof(omod_id), Name = "ix_opreq_omod")]
public partial class op_request
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid request_id { get; set; }

    [Column(TypeName = "char(36)")]
    public Guid requester_id { get; set; }

    [Column(TypeName = "enum('withdraw','rank_up','become_author')")]
    public string request_type { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? request_content { get; set; }

    public ulong? withdraw_amount { get; set; }

    [Column(TypeName = "char(36)")]
    public Guid? omod_id { get; set; }

    [Column(TypeName = "enum('pending','approved','rejected')")]
    public string status { get; set; } = null!;

    [StringLength(64)]
    public string? withdraw_code { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey(nameof(requester_id))]
    public virtual account requester { get; set; } = null!;

    [ForeignKey(nameof(omod_id))]
    public virtual OperationMod? omod { get; set; }
}
