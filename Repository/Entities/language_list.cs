using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Repository.Entities;

[Table("language_list")]
[Index("lang_name", Name = "ux_lang_name", IsUnique = true)]
[Index("lang_code", Name = "ux_language_code", IsUnique = true)]
public partial class language_list
{
    [Key]
    
    public Guid lang_id { get; set; }

    [StringLength(8)]
    public string lang_code { get; set; } = null!;

    [StringLength(64)]
    public string lang_name { get; set; } = null!;

    [InverseProperty("lang")]
    public virtual ICollection<chapter_localization> chapter_localizations { get; set; } = new List<chapter_localization>();

    [InverseProperty("language")]
    public virtual ICollection<story> stories { get; set; } = new List<story>();
}
