using System;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using StackExchange.Redis;

namespace Service.Helpers
{
    public class RedisOtpStore : IOtpStore
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly OtpSettings _opt;
        public RedisOtpStore(IConnectionMultiplexer redis, IOptions<OtpSettings> opt)
        {
            _redis = redis;
            _opt = opt.Value;
        }

        private string Key(string email) => $"{_opt.RedisPrefix}:{email}";
        private string CountKey(string email) => $"{_opt.RedisPrefix}:count:{email}";
        private string ForgotKey(string email) => $"{_opt.RedisPrefix}:forgot:{email}";
        private string EmailChangeKey(Guid accountId) => $"{_opt.RedisPrefix}:emailchange:{accountId}";

        public async Task SaveAsync(string email, string otp, string passwordBcrypt, string username)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(Key(email), $"{otp}|{passwordBcrypt}|{username}", TimeSpan.FromMinutes(_opt.TtlMinutes));
        }

        public async Task<(string Otp, string PasswordBcrypt, string Username)?> GetAsync(string email)
        {
            var db = _redis.GetDatabase();
            var v = await db.StringGetAsync(Key(email));
            if (v.IsNullOrEmpty) return null;
            var parts = v.ToString().Split('|', 3);
            return (parts[0], parts[1], parts[2]);
        }

        public Task<bool> DeleteAsync(string email)
        {
            var db = _redis.GetDatabase();
            return db.KeyDeleteAsync(Key(email));
        }

        public async Task<bool> CanSendAsync(string email)
        {
            var db = _redis.GetDatabase();
            var key = CountKey(email);
            var cnt = await db.StringIncrementAsync(key);
            if (cnt == 1) await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
            return cnt <= _opt.MaxSendPerHour;
        }

        // Forgot password
        public async Task SaveForgotAsync(string email, string otp, string newPasswordBcrypt)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(ForgotKey(email), $"{otp}|{newPasswordBcrypt}", TimeSpan.FromMinutes(_opt.TtlMinutes));
        }

        public async Task<(string Otp, string NewPasswordBcrypt)?> GetForgotAsync(string email)
        {
            var db = _redis.GetDatabase();
            var v = await db.StringGetAsync(ForgotKey(email));
            if (v.IsNullOrEmpty) return null;
            var parts = v.ToString().Split('|', 2);
            return (parts[0], parts[1]);
        }

        public Task<bool> DeleteForgotAsync(string email)
        {
            var db = _redis.GetDatabase();
            return db.KeyDeleteAsync(ForgotKey(email));
        }

        // Change email
        public async Task SaveEmailChangeAsync(Guid accountId, string newEmail, string otp)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(EmailChangeKey(accountId), $"{newEmail}|{otp}", TimeSpan.FromMinutes(_opt.TtlMinutes));
        }

        public async Task<(string NewEmail, string Otp)?> GetEmailChangeAsync(Guid accountId)
        {
            var db = _redis.GetDatabase();
            var v = await db.StringGetAsync(EmailChangeKey(accountId));
            if (v.IsNullOrEmpty) return null;
            var parts = v.ToString().Split('|', 2);
            return (parts[0], parts[1]);
        }

        public Task<bool> DeleteEmailChangeAsync(Guid accountId)
        {
            var db = _redis.GetDatabase();
            return db.KeyDeleteAsync(EmailChangeKey(accountId));
        }
    }
}
