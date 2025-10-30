using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Index("requester_id", Name = "ix_opreq_requester")]
[Index("omod_id", Name = "ix_opreq_omod")]
public partial class op_request
{
    [Key]
    public ulong request_id { get; set; }

    public ulong requester_id { get; set; } // <- đổi tên

    [Column(TypeName = "enum('withdraw','rank_up','become_author')")]
    public string request_type { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? request_content { get; set; }

    public ulong? withdraw_amount { get; set; }

    public ulong? omod_id { get; set; }     // <- nullable

    [Column(TypeName = "enum('pending','approved','rejected')")]
    public string status { get; set; } = null!;

    [StringLength(64)]
    public string? withdraw_code { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    // Navs
    [ForeignKey("requester_id")]
    public virtual account requester { get; set; } = null!;  // <- sang account

    [ForeignKey("omod_id")]
    public virtual OperationMod? omod { get; set; }          // <- nullable
}
