using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterUpdateRequest
    {
        [StringLength(255)]
        public string? Title { get; set; }

        [StringLength(8)]
        public string? LanguageCode { get; set; }

        public string? Content { get; set; }
    }
}

