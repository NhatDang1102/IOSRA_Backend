using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[PrimaryKey("follower_id", "followee_id")]
[Table("follow")]
[Index("followee_id", Name = "ix_follow_followee")]
public partial class follow
{
    [Key]
    
    public Guid follower_id { get; set; }

    [Key]
    
    public Guid followee_id { get; set; }

    [Required]
    public bool? noti_new_story { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("followee_id")]
    [InverseProperty("follows")]
    public virtual author followee { get; set; } = null!;

    [ForeignKey("follower_id")]
    [InverseProperty("follows")]
    public virtual reader follower { get; set; } = null!;
}
