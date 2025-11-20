using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_wallet")]
[Index("account_id", Name = "ux_voice_wallet_account", IsUnique = true)]
public partial class voice_wallet
{
    [Key]
    public Guid wallet_id { get; set; }

    public Guid account_id { get; set; }

    public long balance_chars { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("voice_wallet")]
    public virtual account account { get; set; } = null!;

    [InverseProperty("voice_wallet")]
    public virtual ICollection<voice_payment> voice_payments { get; set; } = new List<voice_payment>();
}
