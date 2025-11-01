using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("OperationMod")]
public partial class OperationMod
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid account_id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime assigned_date { get; set; }

    public uint reports_generated { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("OperationMod")]
    public virtual account account { get; set; } = null!;

    [InverseProperty("omod")]
    public virtual ICollection<op_request> op_requests { get; set; } = new List<op_request>();
}
