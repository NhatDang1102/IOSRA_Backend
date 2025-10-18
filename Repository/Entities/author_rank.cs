using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("author_rank")]
[Index("rank_name", Name = "ux_author_rank_name", IsUnique = true)]
public partial class author_rank
{
    [Key]
    public ushort rank_id { get; set; }

    [StringLength(50)]
    public string rank_name { get; set; } = null!;

    [Precision(5, 2)]
    public decimal reward_rate { get; set; }

    public uint min_followers { get; set; }

    [InverseProperty("rank")]
    public virtual ICollection<author> authors { get; set; } = new List<author>();
}
