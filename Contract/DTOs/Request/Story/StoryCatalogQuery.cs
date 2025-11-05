using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Contract.DTOs.Request.Story
{
    public class StoryCatalogQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        [StringLength(200)]
        public string? Query { get; set; }

        public Guid? TagId { get; set; }

        public Guid? AuthorId { get; set; }
    }
}
