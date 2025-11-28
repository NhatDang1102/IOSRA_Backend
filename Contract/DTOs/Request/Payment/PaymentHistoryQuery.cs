using System;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Payment
{
    public class PaymentHistoryQuery
    {
        private const int DefaultPageSize = 20;

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        public int PageSize { get; set; } = DefaultPageSize;

        public string? Type { get; set; }

        public string? Status { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }
    }
}
