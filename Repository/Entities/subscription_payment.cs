using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("subscription_payment")]
[Index("order_code", Name = "ux_sub_payment_order", IsUnique = true)]
public partial class subscription_payment
{
    [Key]
    public Guid payment_id { get; set; }

    public Guid account_id { get; set; }

    [StringLength(32)]
    public string plan_code { get; set; } = null!;

    [StringLength(50)]
    public string provider { get; set; } = "PayOS";

    [StringLength(50)]
    public string order_code { get; set; } = null!;

    [Column(TypeName = "bigint unsigned")]
    public ulong amount_vnd { get; set; }

    [Column(TypeName = "enum('pending','success','failed','cancelled')")]
    public string status { get; set; } = "pending";

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("subscription_payments")]
    public virtual account account { get; set; } = null!;

    [ForeignKey("plan_code")]
    [InverseProperty("subscription_payments")]
    public virtual subscription_plan plan { get; set; } = null!;
}
