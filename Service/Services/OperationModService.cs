using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Respond.OperationMod;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Implementations
{
    public class OperationModService : IOperationModService
    {
        private readonly IOpRequestRepository _opRepo;

        public OperationModService(IOpRequestRepository opRepo)
        {
            _opRepo = opRepo;
        }

        public async Task<List<OpRequestItemResponse>> ListAsync(string? status, CancellationToken ct = default)
        {
            var data = await _opRepo.ListRequestsAsync(status, ct);
            return data.Select(x => new OpRequestItemResponse
            {
                RequestId = x.request_id,
                RequesterId = x.requester_id,   // ⬅️ map mới
                Status = x.status,
                Content = x.request_content,
                CreatedAt = x.created_at,
                AssignedOmodId = x.omod_id
            }).ToList();
        }

        public async Task ApproveAsync(ulong requestId, ulong omodAccountId, CancellationToken ct = default)
        {
            var req = await _opRepo.GetRequestAsync(requestId, ct)
                      ?? throw new AppException("NotFound", "Không tìm thấy request.", 404);
            if (!string.Equals(req.status, "pending", StringComparison.OrdinalIgnoreCase))
                throw new AppException("InvalidState", "Chỉ duyệt request ở trạng thái pending.", 400);

            var rankId = await _opRepo.GetRankIdByNameAsync("Casual", ct);
            if (rankId == 0) throw new AppException("SeedMissing", "Rank 'Casual' chưa được seed.", 500);

            // Nâng cấp sang Author cho requester
            await _opRepo.EnsureAuthorUpgradedAsync(req.requester_id, rankId, ct);

            var authorRoleId = await _opRepo.GetRoleIdByCodeAsync("author", ct);
            if (authorRoleId == 0) throw new AppException("SeedMissing", "Role 'author' chưa được seed.", 500);

            await _opRepo.AddAccountRoleIfNotExistsAsync(req.requester_id, authorRoleId, ct);

            await _opRepo.SetRequestApprovedAsync(req.request_id, omodAccountId, ct);
        }

        public async Task RejectAsync(ulong requestId, ulong omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default)
        {
            var r = await _opRepo.GetRequestAsync(requestId, ct)
                      ?? throw new AppException("NotFound", "Không tìm thấy request.", 404);
            if (!string.Equals(r.status, "pending", StringComparison.OrdinalIgnoreCase))
                throw new AppException("InvalidState", "Chỉ từ chối request ở trạng thái pending.", 400);

            await _opRepo.SetRequestRejectedAsync(requestId, omodAccountId, req.Reason, ct);
        }
    }
}
