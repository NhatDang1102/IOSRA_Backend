using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Payment;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class PaymentHistoryService : IPaymentHistoryService
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "dia_topup",
            "voice_topup",
            "subscription"
        };

        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "pending",
            "success",
            "failed",
            "refunded",
            "cancelled"
        };

        private readonly IPaymentHistoryRepository _repository;

        public PaymentHistoryService(IPaymentHistoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<PaymentHistoryItemResponse>> GetAsync(Guid accountId, PaymentHistoryQuery query, CancellationToken ct = default)
        {
            if (query == null)
            {
                throw new AppException("ValidationFailed", "Yêu cầu là bắt buộc.", 400);
            }

            if (query.Page < 1)
            {
                throw new AppException("ValidationFailed", "Page phải lớn hơn hoặc bằng 1.", 400);
            }

            if (query.PageSize < 1 || query.PageSize > 200)
            {
                throw new AppException("ValidationFailed", "PageSize phải nằm trong khoảng từ 1 đến 200.", 400);
            }

            ValidateDateRange(query.From, query.To);

            var normalizedType = NormalizeType(query.Type);
            var normalizedStatus = NormalizeStatus(query.Status);

            var (items, total) = await _repository.GetHistoryAsync(
                accountId,
                query.Page,
                query.PageSize,
                normalizedType,
                normalizedStatus,
                query.From,
                query.To,
                ct);

            var mapped = items.Select(r => new PaymentHistoryItemResponse
            {
                PaymentId = r.PaymentId,
                Type = r.Type,
                Provider = r.Provider,
                OrderCode = r.OrderCode,
                AmountVnd = r.AmountVnd,
                GrantedValue = r.GrantedValue,
                GrantedUnit = r.GrantedUnit,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                PlanCode = r.PlanCode,
                PlanName = r.PlanName
            }).ToList();

            return new PagedResult<PaymentHistoryItemResponse>
            {
                Items = mapped,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        private static string? NormalizeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            var normalized = type.Trim().ToLowerInvariant();
            return AllowedTypes.Contains(normalized) ? normalized : throw new AppException("ValidationFailed", $"Loại '{type}' không được hỗ trợ.", 400);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = status.Trim().ToLowerInvariant();
            return AllowedStatuses.Contains(normalized) ? normalized : throw new AppException("ValidationFailed", $"Trạng thái '{status}' không được hỗ trợ.", 400);
        }

        private static void ValidateDateRange(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                throw new AppException("ValidationFailed", "Ngày bắt đầu phải sớm hơn ngày kết thúc.", 400);
            }
        }
    }
}