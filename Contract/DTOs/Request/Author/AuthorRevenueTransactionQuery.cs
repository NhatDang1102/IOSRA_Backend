using System;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Author
{
    public class AuthorRevenueTransactionQuery
    {
        private const int MaxPageSize = 200;

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, MaxPageSize)]
        public int PageSize { get; set; } = 20;

        public string? Type { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }
    }
}
