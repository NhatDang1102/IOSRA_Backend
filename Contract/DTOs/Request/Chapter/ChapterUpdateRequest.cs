namespace Contract.DTOs.Request.Chapter
{
    public class ChapterUpdateRequest
    {
        public string? Title { get; set; }
        public string? LanguageCode { get; set; }
        public string? Content { get; set; }
    }
}
