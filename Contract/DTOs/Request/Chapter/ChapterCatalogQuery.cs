using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Contract.DTOs.Request.Chapter
{
    public class ChapterCatalogQuery
    {
        [Required]
        public Guid StoryId { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        public int PageSize { get; set; } = 50;

        [JsonIgnore]
        public Guid? ViewerAccountId { get; set; }
    }
}
