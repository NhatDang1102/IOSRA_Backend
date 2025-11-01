using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Index("plan_code", Name = "ix_sub_plan")]
[Index("user_id", Name = "ix_sub_user")]
public partial class subcription
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid sub_id { get; set; }

    [Column(TypeName = "char(36)")]
    public Guid user_id { get; set; }

    [StringLength(32)]
    public string plan_code { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime start_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime end_at { get; set; }

    public DateOnly? last_claim_date { get; set; }

    public bool claimed_today { get; set; }

    [ForeignKey("plan_code")]
    [InverseProperty("subcriptions")]
    public virtual subscription_plan plan_codeNavigation { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("subcriptions")]
    public virtual account user { get; set; } = null!;
}
