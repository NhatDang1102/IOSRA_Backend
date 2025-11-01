using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("author")]
[Index("rank_id", Name = "fk_author_rank")]
public partial class author
{
    [Key]
    
    public Guid account_id { get; set; }

    public bool restricted { get; set; }

    
    public Guid? rank_id { get; set; }

    public bool verified_status { get; set; }

    public uint total_story { get; set; }

    public uint total_follower { get; set; }

    [ForeignKey("account_id")]
    [InverseProperty("author")]
    public virtual account account { get; set; } = null!;

    [InverseProperty("followee")]
    public virtual ICollection<follow> follows { get; set; } = new List<follow>();

    [ForeignKey("rank_id")]
    [InverseProperty("authors")]
    public virtual author_rank? rank { get; set; }

    [InverseProperty("author")]
    public virtual ICollection<story> stories { get; set; } = new List<story>();
}
