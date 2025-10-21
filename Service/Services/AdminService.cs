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
        private readonly IAdminRepository _adminRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IAccountRepository _accRepo;

        public AdminService(IAdminRepository adminRepo, IRoleRepository roleRepo, IAccountRepository accRepo)
        {
            _adminRepo = adminRepo;
            _roleRepo = roleRepo;
            _accRepo = accRepo;
        }

        public async Task<PagedResult<AccountAdminResponse>> QueryAccountsAsync(AccountQuery q, CancellationToken ct)
        {
            if (q.Page < 1 || q.PageSize < 1)
                throw new AppException("ValidationFailed", "Tham số phân trang không hợp lệ.");

            var (items, total) = await _adminRepo.QueryAccountsAsync(q, ct);

            var result = new List<AccountAdminResponse>(items.Count);
            foreach (var a in items)
            {
                // dùng repo RoleRepository sẵn có
                var roles = await _roleRepo.GetRoleCodesOfAccountAsync(a.account_id, ct);

                result.Add(new AccountAdminResponse
                {
                    AccountId = a.account_id,
                    Username = a.username,
                    Email = a.email,
                    Status = a.status,
                    Strike = a.strike,
                    CreatedAt = a.created_at,
                    UpdatedAt = a.updated_at,
                    Roles = roles
                });
            }

            return new PagedResult<AccountAdminResponse>
            {
                Items = result,
                Total = total,
                Page = q.Page,
                PageSize = q.PageSize
            };
        }

        public async Task SetRolesAsync(ulong accountId, List<string> roleCodes, CancellationToken ct)
        {
            if (roleCodes is null || roleCodes.Count == 0)
                throw new AppException("ValidationFailed", "Cần cung cấp ít nhất 1 role_code.");

            var acc = await _adminRepo.GetAccountAsync(accountId, ct);
            if (acc is null)
                throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            // Map code -> id, đảm bảo code hợp lệ
            var roleIds = new List<ushort>();
            foreach (var code in roleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var id = await _roleRepo.GetRoleIdByCodeAsync(code, ct);
                if (id == 0)
                    throw new AppException("RoleNotFound", $"role_code '{code}' không tồn tại.", 404);
                roleIds.Add(id);
            }

            await _adminRepo.ReplaceRolesAsync(accountId, roleIds, ct);
        }

        public async Task BanAsync(ulong accountId, string? reason, CancellationToken ct)
        {
            var acc = await _adminRepo.GetAccountAsync(accountId, ct);
            if (acc is null) throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);
            if (acc.status == "banned") return;

            // Không cho ban ADMIN (chính sách mẫu)
            var roles = await _roleRepo.GetRoleCodesOfAccountAsync(accountId, ct);
            if (roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase))
                throw new AppException("Forbidden", "Không thể ban tài khoản ADMIN.", 403, new { reason });

            await _adminRepo.SetStatusAsync(accountId, "banned", ct);
        }

        public async Task UnbanAsync(ulong accountId, string? reason, CancellationToken ct)
        {
            var acc = await _adminRepo.GetAccountAsync(accountId, ct);
            if (acc is null) throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);
            if (acc.status == "unbanned") return;

            await _adminRepo.SetStatusAsync(accountId, "unbanned", ct);
        }

        public async Task<AccountAdminResponse> GetAccountByIdentifierAsync(string identifier, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new AppException("BadRequest", "Thiếu tham số identifier.", 400, new { identifier });

            var acc = await _accRepo.FindByIdentifierAsync(identifier, ct);
            if (acc is null)
                throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404, new { identifier });

            var roles = await _roleRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);

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
