using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("subscription_plan")]
public partial class subscription_plan
{
    [Key]
    [StringLength(32)]
    public string plan_code { get; set; } = null!;

    [StringLength(64)]
    public string plan_name { get; set; } = null!;

    [Column(TypeName = "bigint unsigned")]
    public ulong price_vnd { get; set; }

    public uint daily_claim_limit { get; set; }

    public uint duration_days { get; set; }

    public uint daily_dias { get; set; }

    [InverseProperty("plan_codeNavigation")]
    public virtual ICollection<subscription> subscriptions { get; set; } = new List<subscription>();

    [InverseProperty("plan")]
    public virtual ICollection<subscription_payment> subscription_payments { get; set; } = new List<subscription_payment>();
}
