using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("account")]
[Index("email", Name = "ux_account_email", IsUnique = true)]
[Index("username", Name = "ux_account_username", IsUnique = true)]
public partial class account
{
    [Key]
    public Guid account_id { get; set; }

    [StringLength(50)]
    public string username { get; set; } = null!;

    public string email { get; set; } = null!;

    [StringLength(255)]
    public string password_hash { get; set; } = null!;

    [Column(TypeName = "enum('unbanned','banned')")]
    public string status { get; set; } = null!;

    public byte strike { get; set; }

    [StringLength(512)]
    public string? avatar_url { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [InverseProperty("account")]
    public virtual ContentMod? ContentMod { get; set; }

    [InverseProperty("account")]
    public virtual OperationMod? OperationMod { get; set; }

    [InverseProperty("account")]
    public virtual ICollection<account_role> account_roles { get; set; } = new List<account_role>();

    [InverseProperty("account")]
    public virtual admin? admin { get; set; }

    [InverseProperty("account")]
    public virtual author? author { get; set; }

    [InverseProperty("account")]
    public virtual ICollection<chapter_purchase_log> chapter_purchase_logs { get; set; } = new List<chapter_purchase_log>();

    [InverseProperty("account")]
    public virtual dia_wallet? dia_wallet { get; set; }

    [InverseProperty("account")]
    public virtual reader? reader { get; set; }

    [InverseProperty("reporter")]
    public virtual ICollection<report> reports { get; set; } = new List<report>();

    [InverseProperty("user")]
    public virtual ICollection<subcription> subcriptions { get; set; } = new List<subcription>();
    public virtual ICollection<op_request> op_requests_as_requester { get; set; } = new List<op_request>();
}
