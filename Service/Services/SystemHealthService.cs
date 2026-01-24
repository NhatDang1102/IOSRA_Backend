using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repository.DBContext;
using Service.Interfaces;
using StackExchange.Redis;

namespace Service.Services
{
    public class SystemHealthService : ISystemHealthService
    {
        private readonly AppDbContext _db;
        private readonly IConnectionMultiplexer _redis;
        private readonly IConfiguration _cfg;

        public SystemHealthService(AppDbContext db, IConnectionMultiplexer redis, IConfiguration cfg)
        {
            _db = db;
            _redis = redis;
            _cfg = cfg;
        }

        public async Task<HealthResponse> CheckAsync(CancellationToken ct = default)
        {
            // Health-check mức ứng dụng (phục vụ dashboard vận hành)
            // 1) Database: test kết nối nhanh bằng EF Core CanConnectAsync
            // 2) Redis: kiểm tra trạng thái kết nối từ ConnectionMultiplexer
            // 3) External configs: chỉ xác nhận "đã cấu hình" (OpenAI / Cloudflare R2 / Cloudinary)
            //    => KHÔNG gọi các provider thật để tránh tốn chi phí và tăng latency

            var checkedAtUtc = DateTime.UtcNow;

            // 1) Database
            var dbOk = await _db.Database.CanConnectAsync(ct);

            // 2) Redis
            bool redisOk;
            try
            {
                redisOk = _redis.IsConnected;
            }
            catch
            {
                // Phòng trường hợp Redis client throw exception khi reconnect/disposing
                redisOk = false;
            }

            // 3) External configs (chỉ check tồn tại config)
            var openAiKeyExists = !string.IsNullOrWhiteSpace(_cfg["OpenAi:ApiKey"]);
            var r2BucketExists = !string.IsNullOrWhiteSpace(_cfg["CloudflareR2:Bucket"]);
            var cloudinaryExists = !string.IsNullOrWhiteSpace(_cfg["CloudinarySettings:CloudName"]);

            // Quy ước status:
            // - Healthy: DB + Redis OK
            // - Degraded: một trong hai thành phần cốt lõi gặp vấn đề
            var status = (dbOk && redisOk) ? "Healthy" : "Degraded";


            return new HealthResponse
            {
                Status = status,
                CheckedAtUtc = checkedAtUtc,
                Components = new Dictionary<string, bool>
                {
                    ["api"] = true,
                    ["database"] = dbOk,
                    ["redis"] = redisOk,
                    ["openAiConfigured"] = openAiKeyExists,
                    ["cloudflareR2Configured"] = r2BucketExists,
                    ["cloudinaryConfigured"] = cloudinaryExists
                }
            };
        }
    }
}
