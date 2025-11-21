using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Voice
{
    public class VoiceChapterOrderRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one voice must be selected.")]
        public List<Guid> VoiceIds { get; set; } = new();
    }
}
