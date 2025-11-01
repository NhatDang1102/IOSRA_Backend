using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("roles")]
[Index("role_code", Name = "ux_roles_code", IsUnique = true)]
public partial class role
{
    [Key]
    public Guid role_id { get; set; }

    [StringLength(32)]
    public string role_code { get; set; } = null!;

    [StringLength(64)]
    public string role_name { get; set; } = null!;

    [InverseProperty("role")]
    public virtual ICollection<account_role> account_roles { get; set; } = new List<account_role>();
}
