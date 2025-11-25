using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterTranslationRequest
    {
        [Required]
        [StringLength(16)]
        public string TargetLanguageCode { get; set; } = string.Empty;
    }
}
