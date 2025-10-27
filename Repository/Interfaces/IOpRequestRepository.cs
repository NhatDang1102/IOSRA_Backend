using Repository.Entities;

public interface IOpRequestRepository
{
    // Nộp upgrade request
    Task<op_request> CreateUpgradeRequestAsync(ulong accountId, string? content, CancellationToken ct = default);

    // Check trạng thái request
    Task<bool> HasPendingAsync(ulong accountId, CancellationToken ct = default);
    Task<DateTime?> GetLastRejectedAtAsync(ulong accountId, CancellationToken ct = default);

    // List
    Task<List<op_request>> ListRequestsAsync(string? status, CancellationToken ct = default);
    Task<List<op_request>> ListRequestsOfRequesterAsync(ulong accountId, CancellationToken ct = default);

    // Get chi tiết
    Task<op_request?> GetRequestAsync(ulong requestId, CancellationToken ct = default);

    // Approve / Reject
    Task SetRequestApprovedAsync(ulong requestId, ulong omodId, CancellationToken ct = default);
    Task SetRequestRejectedAsync(ulong requestId, ulong omodId, string? reason, CancellationToken ct = default);

    // Author & ranks
    Task<bool> AuthorIsUnrestrictedAsync(ulong accountId, CancellationToken ct = default);
    Task EnsureAuthorUpgradedAsync(ulong accountId, ushort rankId, CancellationToken ct = default);
    Task<ushort> GetRankIdByNameAsync(string rankName, CancellationToken ct = default);

    // Roles
    Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
    Task AddAccountRoleIfNotExistsAsync(ulong accountId, ushort roleId, CancellationToken ct = default);
}
