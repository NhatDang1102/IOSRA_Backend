using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;
using Repository.DataModels;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class AdminService : IAdminService
    {
        private static readonly string[] AssignableRoles = { "reader", "cmod", "omod", "admin" };
        private static readonly string[] FilterableRoles = { "reader", "author", "cmod", "omod", "admin" };
        private static readonly string[] AllowedStatuses = { "banned", "unbanned" };

        private readonly IAdminRepository _repository;

        public AdminService(IAdminRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<AdminAccountResponse>> GetAccountsAsync(string? status, string? role, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedPage = page <= 0 ? 1 : page;
            var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
            var normalizedStatus = NormalizeStatusFilter(status);
            var normalizedRole = NormalizeRoleFilter(role);

            var (items, total) = await _repository.GetAccountsAsync(normalizedStatus, normalizedRole, normalizedPage, normalizedPageSize, ct);
            var responses = items.Select(Map).ToArray();

            return new PagedResult<AdminAccountResponse>
            {
                Items = responses,
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedPageSize
            };
        }

        public async Task<AdminAccountResponse> AssignRoleAsync(Guid accountId, AssignAdminRoleRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("ValidationFailed", "Request body is required.", 400);
            }

            var normalizedRole = NormalizeRole(request.Role);

            var account = await _repository.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            var hasConflictingRole = account.Roles.Any(r => AssignableRoles.Contains(r, StringComparer.OrdinalIgnoreCase) && !string.Equals(r, normalizedRole, StringComparison.OrdinalIgnoreCase));
            var alreadyHasRole = account.Roles.Any(r => string.Equals(r, normalizedRole, StringComparison.OrdinalIgnoreCase));

            if (alreadyHasRole && !hasConflictingRole)
            {
                return Map(account);
            }

            if (!string.Equals(normalizedRole, "reader", StringComparison.OrdinalIgnoreCase))
            {
                var isAuthor = await _repository.HasAuthorProfileAsync(accountId, ct);
                if (isAuthor)
                {
                    throw new AppException("CannotPromoteAuthor", "Author accounts cannot be promoted to moderator/admin roles.", 409);
                }
            }

            await _repository.RemovePrimaryProfilesAsync(accountId, ct);
            await _repository.RemovePrimaryRolesAsync(accountId, ct);

            switch (normalizedRole)
            {
                case "reader":
                    await _repository.EnsureReaderProfileAsync(accountId, ct);
                    break;
                case "cmod":
                    await _repository.AddContentModProfileAsync(accountId, ct);
                    break;
                case "omod":
                    await _repository.AddOperationModProfileAsync(accountId, ct);
                    break;
                case "admin":
                    await _repository.AddAdminProfileAsync(accountId, ct);
                    break;
            }

            await _repository.AddRoleAsync(accountId, normalizedRole, ct);
            await _repository.SaveChangesAsync(ct);

            var updated = await _repository.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            return Map(updated);
        }

        public async Task<AdminAccountResponse> UpdateStatusAsync(Guid accountId, UpdateAccountStatusRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("ValidationFailed", "Request body is required.", 400);
            }

            var normalizedStatus = NormalizeStatus(request.Status);

            var account = await _repository.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            if (string.Equals(account.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Map(account);
            }

            await _repository.SetAccountStatusAsync(accountId, normalizedStatus, ct);
            await _repository.SaveChangesAsync(ct);

            var updated = await _repository.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            return Map(updated);
        }

        private static string? NormalizeStatusFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!AllowedStatuses.Contains(normalized))
            {
                throw new AppException("InvalidStatus", $"Unsupported status '{value}'.", 400);
            }

            return normalized;
        }

        private static string NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidStatus", "Status is required.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!AllowedStatuses.Contains(normalized))
            {
                throw new AppException("InvalidStatus", $"Unsupported status '{value}'.", 400);
            }

            return normalized;
        }

        private static string? NormalizeRoleFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!FilterableRoles.Contains(normalized))
            {
                throw new AppException("InvalidRole", $"Unsupported role '{value}'.", 400);
            }

            return normalized;
        }

        private static string NormalizeRole(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidRole", "Role is required.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!FilterableRoles.Contains(normalized))
            {
                throw new AppException("InvalidRole", $"Unsupported role '{value}'.", 400);
            }

            return normalized;
        }

        private static AdminAccountResponse Map(AdminAccountProjection projection)
        {
            return new AdminAccountResponse
            {
                AccountId = projection.AccountId,
                Username = projection.Username,
                Email = projection.Email,
                Status = projection.Status,
                Strike = projection.Strike,
                StrikeStatus = projection.StrikeStatus,
                StrikeRestrictedUntil = projection.StrikeRestrictedUntil,
                CreatedAt = projection.CreatedAt,
                UpdatedAt = projection.UpdatedAt,
                Roles = projection.Roles.ToArray()
            };
        }
    }
}
