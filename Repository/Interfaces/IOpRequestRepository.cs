using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

public interface IOpRequestRepository
{
    Task<op_request> CreateUpgradeRequestAsync(Guid accountId, string? content, CancellationToken ct = default);
    Task<bool> HasPendingAsync(Guid accountId, CancellationToken ct = default);
    Task<DateTime?> GetLastRejectedAtAsync(Guid accountId, CancellationToken ct = default);
    Task<List<op_request>> ListRequestsAsync(string? status, CancellationToken ct = default);
    Task<List<op_request>> ListRequestsOfRequesterAsync(Guid accountId, CancellationToken ct = default);
    Task<op_request?> GetRequestAsync(Guid requestId, CancellationToken ct = default);
    Task SetRequestApprovedAsync(Guid requestId, Guid omodId, CancellationToken ct = default);
    Task SetRequestRejectedAsync(Guid requestId, Guid omodId, string? reason, CancellationToken ct = default);
    Task<bool> AuthorIsUnrestrictedAsync(Guid accountId, CancellationToken ct = default);
    Task EnsureAuthorUpgradedAsync(Guid accountId, Guid rankId, CancellationToken ct = default);
    Task<Guid?> GetRankIdByNameAsync(string rankName, CancellationToken ct = default);
    Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
    Task AddAccountRoleIfNotExistsAsync(Guid accountId, Guid roleId, CancellationToken ct = default);
}
