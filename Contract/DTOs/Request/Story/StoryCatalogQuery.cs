using Swashbuckle.AspNetCore.Annotations;
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Contract.DTOs.Request.Story
{
    [SwaggerSchema("Tiêu chí sắp xếp danh sách truyện")]
    public enum StorySortBy
    {
        [EnumMember(Value = "Newest")]
        Newest = 0,
        [EnumMember(Value = "WeeklyViews")]
        WeeklyViews = 1,
        [EnumMember(Value = "TopRated")]
        TopRated = 2,
        [EnumMember(Value = "MostChapters")]
        MostChapters = 3
    }

    [SwaggerSchema("Chiều sắp xếp")]
    public enum SortDir
    {
        [EnumMember(Value = "Asc")]
        Asc = 0,
        [EnumMember(Value = "Desc")]
        Desc = 1
    }

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

        //add filter
        public bool? IsPremium { get; set; }
        public double? MinAvgRating { get; set; }

        //add sort
        public StorySortBy SortBy { get; set; } = StorySortBy.Newest;
        public SortDir SortDir { get; set; } = SortDir.Desc;
    }


}
