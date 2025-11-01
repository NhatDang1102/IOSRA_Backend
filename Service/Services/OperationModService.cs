﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                RequesterId = x.requester_id,
                Status = x.status,
                Content = x.request_content,
                CreatedAt = x.created_at,
                AssignedOmodId = x.omod_id
            }).ToList();
        }

        public async Task ApproveAsync(Guid requestId, Guid omodAccountId, CancellationToken ct = default)
        {
            var request = await _opRepo.GetRequestAsync(requestId, ct)
                          ?? throw new AppException("RequestNotFound", "Upgrade request was not found.", 404);

            if (!string.Equals(request.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be approved.", 400);
            }

            var casualRankId = await _opRepo.GetRankIdByNameAsync("Casual", ct);
            if (casualRankId is null || casualRankId == Guid.Empty)
            {
                throw new AppException("SeedMissing", "Author rank 'Casual' has not been seeded.", 500);
            }

            await _opRepo.EnsureAuthorUpgradedAsync(request.requester_id, casualRankId.Value, ct);

            var authorRoleId = await _opRepo.GetRoleIdByCodeAsync("author", ct);
            if (authorRoleId is null || authorRoleId == Guid.Empty)
            {
                throw new AppException("SeedMissing", "Role 'author' has not been seeded.", 500);
            }

            await _opRepo.AddAccountRoleIfNotExistsAsync(request.requester_id, authorRoleId.Value, ct);
            await _opRepo.SetRequestApprovedAsync(request.request_id, omodAccountId, ct);
        }

        public async Task RejectAsync(Guid requestId, Guid omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default)
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
