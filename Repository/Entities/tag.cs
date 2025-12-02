using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("tag")]
[Index("tag_name", Name = "ux_tag_name", IsUnique = true)]
public partial class tag
{
    [Key]
    
    public Guid tag_id { get; set; }

    [StringLength(64)]
    public string tag_name { get; set; } = null!;

    [InverseProperty("tag")]
    public virtual ICollection<story_tag> story_tags { get; set; } = new List<story_tag>();
}
