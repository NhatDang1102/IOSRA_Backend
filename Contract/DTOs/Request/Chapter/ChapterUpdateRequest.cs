using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterUpdateRequest
    {
        public string? Title { get; set; }
        public string? LanguageCode { get; set; }
        public string? Content { get; set; }
        [RegularExpression("^(?i)(free|dias)$", ErrorMessage = "AccessType must be either 'free' or 'dias'.")]
        public string? AccessType { get; set; }
    }
}
