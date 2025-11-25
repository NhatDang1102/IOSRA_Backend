using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterVoicePurchaseRequest
    {
        [Required]
        [MinLength(1)]
        public List<Guid> VoiceIds { get; set; } = new();
    }
}
