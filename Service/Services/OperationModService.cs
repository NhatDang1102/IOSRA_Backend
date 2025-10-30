using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Respond.OperationMod;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                RequesterId = x.requester_id,
                Status = x.status,
                Content = x.request_content,
                CreatedAt = x.created_at,
                AssignedOmodId = x.omod_id
            }).ToList();
        }

        public async Task ApproveAsync(ulong requestId, ulong omodAccountId, CancellationToken ct = default)
        {
            var req = await _opRepo.GetRequestAsync(requestId, ct)
                      ?? throw new AppException("RequestNotFound", "Upgrade request was not found.", 404);

            if (!string.Equals(req.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be approved.", 400);
            }

            var rankId = await _opRepo.GetRankIdByNameAsync("Casual", ct);
            if (rankId == 0)
            {
                throw new AppException("SeedMissing", "Author rank 'Casual' has not been seeded.", 500);
            }

            await _opRepo.EnsureAuthorUpgradedAsync(req.requester_id, rankId, ct);

            var authorRoleId = await _opRepo.GetRoleIdByCodeAsync("author", ct);
            if (authorRoleId == 0)
            {
                throw new AppException("SeedMissing", "Role 'author' has not been seeded.", 500);
            }

            await _opRepo.AddAccountRoleIfNotExistsAsync(req.requester_id, authorRoleId, ct);

            await _opRepo.SetRequestApprovedAsync(req.request_id, omodAccountId, ct);
        }

        public async Task RejectAsync(ulong requestId, ulong omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default)
        {
            var entity = await _opRepo.GetRequestAsync(requestId, ct)
                         ?? throw new AppException("RequestNotFound", "Upgrade request was not found.", 404);

            if (!string.Equals(entity.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be rejected.", 400);
            }

            await _opRepo.SetRequestRejectedAsync(requestId, omodAccountId, req.Reason, ct);
        }
    }
}
