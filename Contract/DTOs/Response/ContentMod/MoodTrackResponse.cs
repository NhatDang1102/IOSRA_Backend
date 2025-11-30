using System;

namespace Contract.DTOs.Response.ContentMod
{
    public class MoodTrackResponse
    {
        public Guid TrackId { get; set; }
        public string MoodCode { get; set; } = string.Empty;
        public string MoodName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public string PublicUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
