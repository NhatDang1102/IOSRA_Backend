using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Index("author_id", Name = "ix_opreq_author")]
[Index("omod_id", Name = "ix_opreq_omod")]
public partial class op_request
{
    [Key]
    public ulong request_id { get; set; }

    public ulong author_id { get; set; }

    [Column(TypeName = "enum('withdraw','other')")]
    public string request_type { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? request_content { get; set; }

    public ulong? withdraw_amount { get; set; }

    public ulong omod_id { get; set; }

    [Column(TypeName = "enum('pending','approved','rejected')")]
    public string status { get; set; } = null!;

    [StringLength(64)]
    public string? withdraw_code { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("author_id")]
    [InverseProperty("op_requests")]
    public virtual author author { get; set; } = null!;

    [ForeignKey("omod_id")]
    [InverseProperty("op_requests")]
    public virtual OperationMod omod { get; set; } = null!;
}
