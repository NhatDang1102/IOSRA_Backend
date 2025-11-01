using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("wallet_payment")]
[Index("type", Name = "ix_wpay_type")]
[Index("wallet_id", Name = "ix_wpay_wallet")]
public partial class wallet_payment
{
    [Key]
    
    public Guid trs_id { get; set; }

    
    public Guid wallet_id { get; set; }

    [Column(TypeName = "enum('purchase','withdraw','topup','adjust')")]
    public string type { get; set; } = null!;

    public long coin_delta { get; set; }

    public long coin_after { get; set; }

    
    public Guid? ref_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("wallet_id")]
    [InverseProperty("wallet_payments")]
    public virtual dia_wallet wallet { get; set; } = null!;
}
