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

        public async Task<object> CheckAsync(CancellationToken ct = default)
        {
            var checkedAtUtc = DateTime.UtcNow;

            // DB
            var dbOk = await _db.Database.CanConnectAsync(ct);

            // Redis
            bool redisOk;
            try
            {
                redisOk = _redis.IsConnected;
            }
            catch
            {
                redisOk = false;
            }

            // External configs (chỉ validate key tồn tại để dashboard hiển thị)
            var openAiKeyExists = !string.IsNullOrWhiteSpace(_cfg["OpenAi:ApiKey"]);
            var r2BucketExists = !string.IsNullOrWhiteSpace(_cfg["CloudflareR2:Bucket"]);
            var cloudinaryExists = !string.IsNullOrWhiteSpace(_cfg["CloudinarySettings:CloudName"]);

            var status = (dbOk && redisOk) ? "Healthy" : "Degraded";

            return new
            {
                status,
                checkedAtUtc,
                components = new
                {
                    api = true,
                    database = dbOk,
                    redis = redisOk,
                    openAiConfigured = openAiKeyExists,
                    cloudflareR2Configured = r2BucketExists,
                    cloudinaryConfigured = cloudinaryExists
                }
            };
        }
    }
}
