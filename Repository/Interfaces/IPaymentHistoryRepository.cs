using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.DataModels;

namespace Repository.Interfaces
{
    public interface IPaymentHistoryRepository
    {
        Task<(List<PaymentHistoryRecord> Items, int Total)> GetHistoryAsync(
            Guid accountId,
            int page,
            int pageSize,
            string? type,
            string? status,
            DateTime? from,
            DateTime? to,
            CancellationToken ct = default);
    }
}
