using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("chapter_comment_reaction")]
[Index("comment_id", Name = "ix_ccr_comment")]
[Index("reader_id", Name = "ix_ccr_reader")]
[Index("comment_id", "reader_id", Name = "ux_ccr_comment_reader", IsUnique = true)]
public partial class chapter_comment_reaction
{
    [Key]
    public Guid reaction_id { get; set; }

    public Guid comment_id { get; set; }

    public Guid reader_id { get; set; }

    [Column(TypeName = "enum('like','dislike')")]
    public string reaction_type { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime updated_at { get; set; }

    [ForeignKey("comment_id")]
    [InverseProperty("chapter_comment_reactions")]
    public virtual chapter_comment comment { get; set; } = null!;

    [ForeignKey("reader_id")]
    [InverseProperty("chapter_comment_reactions")]
    public virtual reader reader { get; set; } = null!;
}
