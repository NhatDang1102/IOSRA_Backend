﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("dia_payment")]
[Index("wallet_id", Name = "ix_topup_wallet")]
public partial class dia_payment
{
    [Key]
    public ulong topup_id { get; set; }

    public ulong wallet_id { get; set; }

    [StringLength(50)]
    public string provider { get; set; } = null!;

    public ulong amount_vnd { get; set; }

    public ulong diamond_granted { get; set; }

    [Column(TypeName = "enum('pending','success','failed','refunded')")]
    public string status { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("wallet_id")]
    [InverseProperty("dia_payments")]
    public virtual dia_wallet wallet { get; set; } = null!;
}
