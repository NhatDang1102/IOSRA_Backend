using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Entities;

[Table("story_rating")]
public partial class story_rating
{
    public Guid story_id { get; set; }

    public Guid reader_id { get; set; }

    public byte score { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("reader_id")]
    [InverseProperty("story_ratings")]
    public virtual reader reader { get; set; } = null!;

    [ForeignKey("story_id")]
    [InverseProperty("story_ratings")]
    public virtual story story { get; set; } = null!;
}
