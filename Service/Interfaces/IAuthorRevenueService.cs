using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.Common;

namespace Service.Interfaces
{
    public interface IAuthorRevenueService
    {
        Task<AuthorRevenueSummaryResponse> GetSummaryAsync(Guid authorAccountId, CancellationToken ct = default);
        Task<PagedResult<AuthorRevenueTransactionItemResponse>> GetTransactionsAsync(Guid authorAccountId, AuthorRevenueTransactionQuery query, CancellationToken ct = default);
        Task<AuthorWithdrawRequestResponse> SubmitWithdrawAsync(Guid authorAccountId, AuthorWithdrawRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<AuthorWithdrawRequestResponse>> GetWithdrawRequestsAsync(Guid authorAccountId, string? status, CancellationToken ct = default);
    }
}
