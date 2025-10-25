﻿using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly AppDbContext _db;
        public ProfileRepository(AppDbContext db) => _db = db;

        public Task<account?> GetAccountByIdAsync(ulong accountId, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.account_id == accountId, ct);

        public Task<reader?> GetReaderByIdAsync(ulong accountId, CancellationToken ct = default)
            => _db.readers.FirstOrDefaultAsync(r => r.account_id == accountId, ct);

        public async Task UpdateReaderProfileAsync(ulong accountId, string? bio, string? gender, DateOnly? birthday, CancellationToken ct = default)
        {
            var r = await _db.readers.FirstAsync(x => x.account_id == accountId, ct);
            if (bio != null) r.bio = bio;
            if (gender != null) r.gender = gender;
            if (birthday.HasValue) r.birthdate = birthday;
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAvatarUrlAsync(ulong accountId, string avatarUrl, CancellationToken ct = default)
        {
            var acc = await _db.accounts.FirstAsync(a => a.account_id == accountId, ct);
            acc.avatar_url = avatarUrl;
            await _db.SaveChangesAsync(ct);
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
            => _db.accounts.AnyAsync(a => a.email == email, ct);

        public async Task UpdateEmailAsync(ulong accountId, string newEmail, CancellationToken ct = default)
        {
            var acc = await _db.accounts.FirstAsync(a => a.account_id == accountId, ct);
            acc.email = newEmail;
            await _db.SaveChangesAsync(ct);
        }
    }
}
