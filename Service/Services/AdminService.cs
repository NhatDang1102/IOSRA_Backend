using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    /// <summary>
    /// Xử lý logic quản trị: phân trang, validate role, ban/unban.
    /// </summary>
    public sealed class AdminService : IAdminService
    {
        private readonly IAdminRepository _admins;
        private readonly IRoleRepository _roles;
        private readonly IAccountRepository _accounts;

        public AdminService(IAdminRepository admins, IRoleRepository roles, IAccountRepository accounts)
        {
            _admins = admins;
            _roles = roles;
            _accounts = accounts;
        }

        public async Task<PagedResult<AccountAdminResponse>> QueryAccountsAsync(AccountQuery query, CancellationToken token)
        {
            if (query.Page < 1 || query.PageSize < 1)
                throw new AppException("ValidationFailed", "Tham số phân trang không hợp lệ.");

            var (items, total) = await _admins.QueryAccountsAsync(query, token);

            var result = new List<AccountAdminResponse>(items.Count);
            foreach (var account in items)
            {
                // dùng repo RoleRepository sẵn có
                var roles = await _roles.GetRoleCodesOfAccountAsync(account.account_id, token);

                result.Add(new AccountAdminResponse
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
                Items = result,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task SetRolesAsync(ulong accountId, List<string> roleCodes, CancellationToken token)
        {
            if (roleCodes is null || roleCodes.Count == 0)
                throw new AppException("ValidationFailed", "Cần cung cấp ít nhất 1 role_code.");

            var acc = await _admins.GetAccountAsync(accountId, token);
            if (acc is null)
                throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            // Map code -> id, đảm bảo code hợp lệ
            var roleIds = new List<ushort>();
            foreach (var code in roleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var id = await _roles.GetRoleIdByCodeAsync(code, token);
                if (id == 0)
                    throw new AppException("RoleNotFound", $"role_code '{code}' không tồn tại.", 404);
                roleIds.Add(id);
            }

            await _admins.ReplaceRolesAsync(accountId, roleIds, token);
        }

        public async Task BanAsync(ulong accountId, string? reason, CancellationToken token)
        {
            var acc = await _admins.GetAccountAsync(accountId, token);
            if (acc is null) throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);
            if (acc.status == "banned") return;

            // Không cho ban ADMIN (chính sách mẫu)
            var roles = await _roles.GetRoleCodesOfAccountAsync(accountId, token);
            if (roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase))
                throw new AppException("Forbidden", "Không thể ban tài khoản ADMIN.", 403, new { reason });

            await _admins.SetStatusAsync(accountId, "banned", token);
        }

        public async Task UnbanAsync(ulong accountId, string? reason, CancellationToken token)
        {
            var acc = await _admins.GetAccountAsync(accountId, token);
            if (acc is null) throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);
            if (acc.status == "unbanned") return;

            await _admins.SetStatusAsync(accountId, "unbanned", token);
        }

        public async Task<AccountAdminResponse> GetAccountByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new AppException("BadRequest", "Thiếu tham số identifier.", 400, new { identifier });

            var acc = await _accounts.FindByIdentifierAsync(identifier, token);
            if (acc is null)
                throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404, new { identifier });

            var roles = await _roles.GetRoleCodesOfAccountAsync(acc.account_id, token);

            return new AccountAdminResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Status = acc.status,
                Strike = acc.strike,
                CreatedAt = acc.created_at,
                UpdatedAt = acc.updated_at,
                Roles = roles
            };
        }
    }
}
