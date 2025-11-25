using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("author_revenue_transactions")]
[Index(nameof(author_id), Name = "ix_art_author")]
[Index(nameof(purchase_log_id), Name = "ix_art_purchase")]
[Index(nameof(request_id), Name = "ix_art_request")]
public partial class author_revenue_transaction
{
    [Key]
    public Guid trans_id { get; set; }

    public Guid author_id { get; set; }

    [Column(TypeName = "enum('purchase','withdraw_reserve','withdraw_release','withdraw_complete')")]
    public string type { get; set; } = null!;

    [Column(TypeName = "bigint")]
    public long amount_vnd { get; set; }

    public Guid? purchase_log_id { get; set; }

    public Guid? voice_purchase_id { get; set; }

    public Guid? request_id { get; set; }

    [Column(TypeName = "json")]
    public string? metadata { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey(nameof(author_id))]
    [InverseProperty("author_revenue_transactions")]
    public virtual author author { get; set; } = null!;

    [ForeignKey(nameof(purchase_log_id))]
    [InverseProperty("author_revenue_transactions")]
    public virtual chapter_purchase_log? purchase_log { get; set; }

    [ForeignKey(nameof(voice_purchase_id))]
    [InverseProperty("author_revenue_transactions")]
    public virtual voice_purchase_log? voice_purchase { get; set; }

    [ForeignKey(nameof(request_id))]
    [InverseProperty("author_revenue_transactions")]
    public virtual op_request? request { get; set; }
}
