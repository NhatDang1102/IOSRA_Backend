using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bc = BCrypt.Net.BCrypt;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;
using Repository.DataModels;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

using Microsoft.Extensions.DependencyInjection;

namespace Service.Services
{
    public class AdminService : IAdminService
    {
        private static readonly string[] FilterableRoles = { "reader", "author", "cmod", "omod", "admin" };
        private static readonly string[] AllowedStatuses = { "banned", "unbanned" };

        private readonly IAdminRepository _repository;

        public AdminService(IAdminRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<AdminAccountResponse>> GetAccountsAsync(string? status, string? role, string? search, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedPage = page <= 0 ? 1 : page;
            var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
            var normalizedStatus = NormalizeStatusFilter(status);
            var normalizedRole = NormalizeRoleFilter(role);
            var normalizedSearch = search?.Trim();

            var (items, total) = await _repository.GetAccountsAsync(normalizedStatus, normalizedRole, normalizedSearch, normalizedPage, normalizedPageSize, ct);
            var responses = items.Select(Map).ToArray();

            return new PagedResult<AdminAccountResponse>
            {
                Items = responses,
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedPageSize
            };
        }

        public Task<AdminAccountResponse> CreateContentModAsync(CreateModeratorRequest request, CancellationToken ct = default)
            => CreateModeratorAsync(request, "cmod", _repository.AddContentModProfileAsync, ct);

        public Task<AdminAccountResponse> CreateOperationModAsync(CreateModeratorRequest request, CancellationToken ct = default)
            => CreateModeratorAsync(request, "omod", _repository.AddOperationModProfileAsync, ct);

        public async Task<AdminAccountResponse> UpdateStatusAsync(Guid accountId, UpdateAccountStatusRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new AppException("ValidationFailed", "Nội dung yêu cầu là bắt buộc.", 400);
            }

            var normalizedStatus = NormalizeStatus(request.Status);

            var account = await _repository.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Tài khoản ko tồn tại.", 404);

            if (string.Equals(account.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Map(account);
            }

            await _repository.SetAccountStatusAsync(accountId, normalizedStatus, ct);
            await _repository.SaveChangesAsync(ct);

            var updated = await _repository.GetAccountAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Tài khoản ko tồn tại.", 404);

            return Map(updated);
        }

        // Tạo tài khoản Moderator (Content Mod hoặc Operation Mod)
        // 1. Validate Email/Username xem đã tồn tại chưa.
        // 2. Tạo Entity Account với password được hash bằng BCrypt.
        // 3. Tạo Profile tương ứng (ContentModProfile hoặc OperationModProfile).
        // 4. Gán Role hệ thống (cmod/omod).
        private async Task<AdminAccountResponse> CreateModeratorAsync(
            CreateModeratorRequest? request,
            string roleCode,
            Func<Guid, string?, CancellationToken, Task> profileFactory,
            CancellationToken ct)
        {
            if (request == null)
            {
                throw new AppException("ValidationFailed", "Nội dung yêu cầu là bắt buộc.", 400);
            }

            // Chuẩn hóa và kiểm tra dữ liệu đầu vào
            var (email, username, password, phone) = NormalizeModeratorRequest(request);

            if (await _repository.EmailExistsAsync(email, ct))
            {
                throw new AppException("EmailExists", "Email đã được đăng kí.", 409);
            }

            if (await _repository.UsernameExistsAsync(username, ct))
            {
                throw new AppException("UsernameExists", "Username đã được đăng kí.", 409);
            }

            var now = Repository.Utils.TimezoneConverter.VietnamNow;
            var accountId = Guid.NewGuid();
            
            // Tạo tài khoản mới
            var accountEntity = new account
            {
                account_id = accountId,
                username = username,
                email = email,
                password_hash = Bc.HashPassword(password), // Mã hóa mật khẩu
                status = "unbanned",
                strike_status = "none",
                strike = 0,
                strike_restricted_until = null,
                avatar_url = null,
                created_at = now,
                updated_at = now
            };

            await _repository.AddAccountAsync(accountEntity, ct);
            
            // Gọi factory để tạo profile đặc thù cho từng loại mod
            await profileFactory(accountId, phone, ct);
            
            // Gán quyền truy cập hệ thống
            await _repository.AddRoleAsync(accountId, roleCode, ct);
            await _repository.SaveChangesAsync(ct);

            // Trả về thông tin tài khoản vừa tạo
            var projection = await _repository.GetAccountAsync(accountId, ct)
                              ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản sau khi tạo.", 404);
            return Map(projection);
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
                throw new AppException("InvalidStatus", $"Trạng thái '{value}' không được hỗ trợ.", 400);
            }

            return normalized;
        }

        private static string NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidStatus", "Trạng thái là bắt buộc.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!AllowedStatuses.Contains(normalized))
            {
                throw new AppException("InvalidStatus", $"Trạng thái '{value}' không được hỗ trợ.", 400);
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
                throw new AppException("InvalidRole", $"Vai trò '{value}' không được hỗ trợ.", 400);
            }

            return normalized;
        }

        private static (string Email, string Username, string Password, string? Phone) NormalizeModeratorRequest(CreateModeratorRequest request)
        {
            var email = request.Email?.Trim();
            var username = request.Username?.Trim();
            var password = request.Password?.Trim();
            var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone!.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new AppException("InvalidEmail", "Email không được trống.", 400);
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new AppException("InvalidUsername", "Username không được trống.", 400);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new AppException("InvalidPassword", "Password không được trống.", 400);
            }

            return (email, username, password, phone);
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
