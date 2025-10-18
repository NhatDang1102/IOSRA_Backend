using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("dia_wallet")]
[Index("account_id", Name = "ux_wallet_account", IsUnique = true)]
public partial class dia_wallet
{
    [Key]
    public ulong wallet_id { get; set; }

    public ulong account_id { get; set; }

    public long balance_coin { get; set; }

    public long locked_coin { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("dia_wallet")]
    public virtual account account { get; set; } = null!;

    [InverseProperty("wallet")]
    public virtual ICollection<dia_payment> dia_payments { get; set; } = new List<dia_payment>();

    [InverseProperty("wallet")]
    public virtual ICollection<wallet_payment> wallet_payments { get; set; } = new List<wallet_payment>();
}
