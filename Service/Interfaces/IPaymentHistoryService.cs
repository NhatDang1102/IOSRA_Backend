using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Payment;
using Contract.DTOs.Response.Common;

namespace Service.Interfaces
{
    public interface IPaymentHistoryService
    {
        Task<PagedResult<PaymentHistoryItemResponse>> GetAsync(Guid accountId, PaymentHistoryQuery query, CancellationToken ct = default);
    }
}
