using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    // Interface cho repository truy xuất dữ liệu liên quan đến authentication
    public interface IAuthRepository
    {
        // Kiểm tra username hoặc email đã tồn tại chưa
        Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default);

        // Thêm account mới vào database
        Task<account> AddAccountAsync(account entity, CancellationToken ct = default);

        // Tìm account theo identifier (có thể là email hoặc username)
        Task<account?> FindAccountByIdentifierAsync(string identifier, CancellationToken ct = default);

        // Tìm account theo email
        Task<account?> FindAccountByEmailAsync(string email, CancellationToken ct = default);

        // Tìm account theo accountId
        Task<account?> FindAccountByIdAsync(Guid accountId, CancellationToken ct = default);

        // Cập nhật password hash cho account
        Task UpdatePasswordHashAsync(Guid accountId, string newHash, CancellationToken ct = default);

        // Thêm bản ghi reader mới
        Task<reader> AddReaderAsync(reader entity, CancellationToken ct = default);

        // Lấy role ID từ role code (ví dụ: "reader", "author", "admin")
        Task<Guid> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);

        // Lấy danh sách role codes của một account
        Task<List<string>> GetRoleCodesOfAccountAsync(Guid accountId, CancellationToken ct = default);

        // Gán role cho account (thêm vào bảng account_role)
        Task AddAccountRoleAsync(Guid accountId, Guid roleId, CancellationToken ct = default);
    }
}
