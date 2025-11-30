using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.ContentMod
{
    public class MoodTrackUpdateRequest
    {
        [StringLength(128)]
        public string? Title { get; set; }
    }
}
