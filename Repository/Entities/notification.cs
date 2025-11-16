using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Entities;

[Table("notifications")]
public partial class notification
{
    [Key]
    
    public Guid notification_id { get; set; }

    [Required]
    
    public Guid recipient_id { get; set; }

    [Required]
    [Column(TypeName = "enum('op_request','story_decision','chapter_decision','new_story','new_chapter','general','new_follower','chapter_comment','story_rating','strike_warning')")]
    public string type { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string title { get; set; } = null!;

    [Column(TypeName = "text")]
    public string message { get; set; } = null!;

    [Column(TypeName = "json")]
    public string? payload { get; set; }

    public bool is_read { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime created_at { get; set; }

    [ForeignKey("recipient_id")]
    public virtual account recipient { get; set; } = null!;
}
