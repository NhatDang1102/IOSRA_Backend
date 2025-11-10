using System;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Story
{
    public enum StorySortBy
    {
        Newest,
        WeeklyViews,
        TopRated,
        MostChapters
    }

    public enum SortDir
    {
        Asc,
        Desc
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
        public bool? isPremium { get; set; }
        public double? MinAvgRating { get; set; }

        //add sort
        public StorySortBy SortBy { get; set; } = StorySortBy.Newest;
        public SortDir SortDir { get; set; } = SortDir.Desc;
    }


}
