using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using Contract.DTOs.Respond.Common;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    /// <summary>
    /// Application services for admin account management.
    /// </summary>
    public sealed class AdminService : IAdminService
    {
        private readonly IAdminRepository _adminRepo;
        private readonly IAuthRepository _authRepo;

        public AdminService(IAdminRepository adminRepo, IAuthRepository authRepo)
        {
            _adminRepo = adminRepo;
            _authRepo = authRepo;
        }

        public async Task<PagedResult<AccountAdminResponse>> QueryAccountsAsync(AccountQuery q, CancellationToken ct)
        {
            if (q.Page < 1 || q.PageSize < 1)
            {
                throw new AppException("ValidationFailed", "Page and pageSize must be positive integers.", 400);
            }

            var (items, total) = await _adminRepo.QueryAccountsAsync(q, ct);
            var results = new List<AccountAdminResponse>(items.Count);

            foreach (var account in items)
            {
                var roles = await _authRepo.GetRoleCodesOfAccountAsync(account.account_id, ct);
                results.Add(new AccountAdminResponse
                {
                    AccountId = account.account_id,
                    Username = account.username,
                    Email = account.email,
                    Status = account.status,
                    Strike = account.strike,
                    CreatedAt = account.created_at,
                    UpdatedAt = account.updated_at,
                    Roles = roles
                });
            }

            return new PagedResult<AccountAdminResponse>
            {
                Items = results,
                Total = total,
                Page = q.Page,
                PageSize = q.PageSize
            };
        }

        public async Task SetRolesAsync(Guid accountId, List<string> roleCodes, CancellationToken ct)
        {
            if (roleCodes == null || roleCodes.Count == 0)
            {
                throw new AppException("ValidationFailed", "At least one role code is required.", 400);
            }

            var account = await _adminRepo.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("NotFound", "Account was not found.", 404);

            var resolvedRoleIds = new List<Guid>();
            foreach (var code in roleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var roleId = await _authRepo.GetRoleIdByCodeAsync(code, ct);
                if (roleId == Guid.Empty)
                {
                    throw new AppException("RoleNotFound", $"Role code '{code}' does not exist.", 404);
                }
                resolvedRoleIds.Add(roleId);
            }

            await _adminRepo.ReplaceRolesAsync(accountId, resolvedRoleIds, ct);
        }

        public async Task BanAsync(Guid accountId, string? reason, CancellationToken ct)
        {
            var account = await _adminRepo.GetAccountAsync(accountId, ct)
                           ?? throw new AppException("NotFound", "Account was not found.", 404);

            if (account.status == "banned")
            {
                return;
            }

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(accountId, ct);
            if (roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("Forbidden", "Cannot ban an ADMIN account.", 403, new { reason });
            }

            await _adminRepo.SetStatusAsync(accountId, "banned", ct);
        }

        public async Task UnbanAsync(Guid accountId, string? reason, CancellationToken ct)
        {
            var account = await _adminRepo.GetAccountAsync(accountId, ct)
                           ?? throw new AppException("NotFound", "Account was not found.", 404);

            if (account.status == "unbanned")
            {
                return;
            }

            await _adminRepo.SetStatusAsync(accountId, "unbanned", ct);
        }

        public async Task<AccountAdminResponse> GetAccountByIdentifierAsync(string identifier, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new AppException("BadRequest", "Identifier is required.", 400, new { identifier });
            }

            var account = await _authRepo.FindAccountByIdentifierAsync(identifier, ct)
                          ?? throw new AppException("AccountNotFound", "Account was not found.", 404, new { identifier });

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(account.account_id, ct);

            return new AccountAdminResponse
            {
                AccountId = account.account_id,
                Username = account.username,
                Email = account.email,
                Status = account.status,
                Strike = account.strike,
                CreatedAt = account.created_at,
                UpdatedAt = account.updated_at,
                Roles = roles
            };
        }
    }
}
