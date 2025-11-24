using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.OperationMod;

namespace Service.Interfaces
{
    public interface IOperationModStatService
    {
        Task<OperationRevenueResponse> GetRevenueStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<OperationRequestStatResponse> GetRequestStatsAsync(string requestType, StatQueryRequest query, CancellationToken ct = default);
        Task<OperationAuthorRevenueResponse> GetAuthorRevenueStatsAsync(string metric, StatQueryRequest query, CancellationToken ct = default);
    }
}
