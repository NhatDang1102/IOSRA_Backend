using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("admin")]
public partial class admin
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid account_id { get; set; }

    [StringLength(100)]
    public string? department { get; set; }

    [Column(TypeName = "text")]
    public string? notes { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("admin")]
    public virtual account account { get; set; } = null!;
}
