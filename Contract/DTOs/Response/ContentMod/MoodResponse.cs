namespace Contract.DTOs.Response.ContentMod
{
    public class MoodResponse
    {
        public string MoodCode { get; set; } = string.Empty;
        public string MoodName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int TrackCount { get; set; }
    }
}
