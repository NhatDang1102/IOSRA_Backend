using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[PrimaryKey("account_id", "role_id")]
[Index("role_id", Name = "fk_account_roles_role")]
public partial class account_role
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid account_id { get; set; }

    [Key]
    [Column(TypeName = "char(36)")]
    public Guid role_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("account_roles")]
    public virtual account account { get; set; } = null!;

    [ForeignKey("role_id")]
    [InverseProperty("account_roles")]
    public virtual role role { get; set; } = null!;
}
