using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Admin;

namespace Service.Interfaces
{
    public interface ISystemHealthService
    {
        // Health-check mức ứng dụng cho dashboard vận hành (Admin IT / OMOD)
        // - Nhanh, nhẹ: không gọi external API thật, chỉ kiểm tra kết nối DB/Redis và việc cấu hình key
        // - Trả về HealthResponse để Swagger/FE có schema ổn định
        Task<HealthResponse> CheckAsync(CancellationToken ct = default);
    }
}
