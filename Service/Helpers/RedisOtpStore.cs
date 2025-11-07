using System;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using StackExchange.Redis;

namespace Service.Helpers
{
    // Service lưu trữ OTP trong Redis cho các chức năng: đăng ký, quên mật khẩu, đổi email
    public class RedisOtpStore : IOtpStore
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly OtpSettings _opt; // Cấu hình: TTL, max send per hour, prefix
        public RedisOtpStore(IConnectionMultiplexer redis, IOptions<OtpSettings> opt)
        {
            _redis = redis;
            _opt = opt.Value;
        }

        // Các helper methods tạo Redis key
        private string Key(string email) => $"{_opt.RedisPrefix}:{email}"; // Key cho OTP đăng ký
        private string CountKey(string email) => $"{_opt.RedisPrefix}:count:{email}"; // Key đếm số lần gửi OTP
        private string ForgotKey(string email) => $"{_opt.RedisPrefix}:forgot:{email}"; // Key cho OTP quên mật khẩu
        private string EmailChangeKey(Guid accountId) => $"{_opt.RedisPrefix}:emailchange:{accountId}"; // Key cho OTP đổi email

        // Lưu OTP đăng ký kèm password hash và username vào Redis
        public async Task SaveAsync(string email, string otp, string passwordBcrypt, string username)
        {
            var db = _redis.GetDatabase();
            // Lưu dạng pipe-separated: otp|passwordHash|username với TTL từ config
            await db.StringSetAsync(Key(email), $"{otp}|{passwordBcrypt}|{username}", TimeSpan.FromMinutes(_opt.TtlMinutes));
        }

        // Lấy thông tin OTP đăng ký từ Redis
        public async Task<(string Otp, string PasswordBcrypt, string Username)?> GetAsync(string email)
        {
            var db = _redis.GetDatabase();
            var v = await db.StringGetAsync(Key(email));
            if (v.IsNullOrEmpty) return null;
            // Parse pipe-separated value
            var parts = v.ToString().Split('|', 3);
            return (parts[0], parts[1], parts[2]);
        }

        // Xóa OTP đăng ký sau khi verify thành công
        public Task<bool> DeleteAsync(string email)
        {
            var db = _redis.GetDatabase();
            return db.KeyDeleteAsync(Key(email));
        }

        // Kiểm tra rate limit: cho phép gửi tối đa X lần trong 1 giờ
        public async Task<bool> CanSendAsync(string email)
        {
            var db = _redis.GetDatabase();
            var key = CountKey(email);
            // Increment counter
            var cnt = await db.StringIncrementAsync(key);
            // Lần đầu tiên thì set TTL 1 giờ
            if (cnt == 1) await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
            return cnt <= _opt.MaxSendPerHour;
        }

        // === Forgot Password Functions ===

        // Lưu OTP quên mật khẩu vào Redis
        public async Task SaveForgotAsync(string email, string otp, string newPasswordBcrypt)
        {
            var db = _redis.GetDatabase();
            // Lưu otp|newPasswordHash (hiện tại newPasswordHash để empty, sẽ hash sau khi verify)
            await db.StringSetAsync(ForgotKey(email), $"{otp}|{newPasswordBcrypt}", TimeSpan.FromMinutes(_opt.TtlMinutes));
        }

        // Lấy OTP quên mật khẩu từ Redis
        public async Task<(string Otp, string NewPasswordBcrypt)?> GetForgotAsync(string email)
        {
            var db = _redis.GetDatabase();
            var v = await db.StringGetAsync(ForgotKey(email));
            if (v.IsNullOrEmpty) return null;
            var parts = v.ToString().Split('|', 2);
            return (parts[0], parts[1]);
        }

        // Xóa OTP quên mật khẩu sau khi verify
        public Task<bool> DeleteForgotAsync(string email)
        {
            var db = _redis.GetDatabase();
            return db.KeyDeleteAsync(ForgotKey(email));
        }

        // === Change Email Functions ===

        // Lưu OTP đổi email vào Redis (key theo accountId)
        public async Task SaveEmailChangeAsync(Guid accountId, string newEmail, string otp)
        {
            var db = _redis.GetDatabase();
            // Lưu newEmail|otp
            await db.StringSetAsync(EmailChangeKey(accountId), $"{newEmail}|{otp}", TimeSpan.FromMinutes(_opt.TtlMinutes));
        }

        // Lấy thông tin OTP đổi email từ Redis
        public async Task<(string NewEmail, string Otp)?> GetEmailChangeAsync(Guid accountId)
        {
            var db = _redis.GetDatabase();
            var v = await db.StringGetAsync(EmailChangeKey(accountId));
            if (v.IsNullOrEmpty) return null;
            var parts = v.ToString().Split('|', 2);
            return (parts[0], parts[1]);
        }

        // Xóa OTP đổi email sau khi verify
        public Task<bool> DeleteEmailChangeAsync(Guid accountId)
        {
            var db = _redis.GetDatabase();
            return db.KeyDeleteAsync(EmailChangeKey(accountId));
        }
    }
}
