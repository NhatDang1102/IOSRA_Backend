using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("op_requests")]
[Index(nameof(requester_id), Name = "ix_opreq_requester")]
[Index(nameof(omod_id), Name = "ix_opreq_omod")]
public partial class op_request
{
    [Key]
    
    public Guid request_id { get; set; }

    
    public Guid requester_id { get; set; }

    [Column(TypeName = "enum('withdraw','rank_up','become_author','author_withdraw')")]
    public string request_type { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? request_content { get; set; }

    public ulong? withdraw_amount { get; set; }

    
    public Guid? omod_id { get; set; }

    [Column(TypeName = "text")]
    public string? omod_note { get; set; }

    [Column(TypeName = "enum('pending','approved','rejected')")]
    public string status { get; set; } = null!;

    [StringLength(64)]
    public string? withdraw_code { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? reviewed_at { get; set; }

    [ForeignKey(nameof(requester_id))]
    public virtual account requester { get; set; } = null!;

    [ForeignKey(nameof(omod_id))]
    public virtual OperationMod? omod { get; set; }

    [InverseProperty("request")]
    public virtual ICollection<author_revenue_transaction> author_revenue_transactions { get; set; } = new List<author_revenue_transaction>();
}
