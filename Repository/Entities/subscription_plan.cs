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

    public uint price_coin { get; set; }

    public uint daily_claim_limit { get; set; }

    public uint duration_days { get; set; }

    [InverseProperty("plan_codeNavigation")]
    public virtual ICollection<subcription> subcriptions { get; set; } = new List<subcription>();
}
