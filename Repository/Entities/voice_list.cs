using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("voice_list")]
[Index("voice_name", Name = "ux_voice_name", IsUnique = true)]
public partial class voice_list
{
    [Key]
    
    public Guid voice_id { get; set; }

    [StringLength(64)]
    public string voice_name { get; set; } = null!;

    [InverseProperty("voice")]
    public virtual ICollection<chapter_voice> chapter_voices { get; set; } = new List<chapter_voice>();
}
