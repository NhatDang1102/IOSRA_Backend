using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;

namespace Repository.Repositories
{
    public class AuthorFollowRepository : IAuthorFollowRepository
    {
        private readonly AppDbContext _db;

        public AuthorFollowRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<follow?> GetAsync(Guid readerId, Guid authorId, CancellationToken ct = default)
        {
            return _db.follows
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.follower_id == readerId && f.followee_id == authorId, ct);
        }

        public async Task<follow> AddAsync(Guid readerId, Guid authorId, bool enableNotifications, CancellationToken ct = default)
        {
            var entity = new follow
            {
                follower_id = readerId,
                followee_id = authorId,
                noti_new_story = enableNotifications,
                created_at = TimezoneConverter.VietnamNow
            };

            _db.follows.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task RemoveAsync(follow entity, CancellationToken ct = default)
        {
            _db.follows.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<follow> UpdateNotificationAsync(follow entity, bool enableNotifications, CancellationToken ct = default)
        {
            entity.noti_new_story = enableNotifications;
            _db.follows.Update(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<(IReadOnlyList<AuthorFollowerProjection> Items, int Total)> GetFollowersAsync(Guid authorId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.follows
                .AsNoTracking()
                .Where(f => f.followee_id == authorId)
                .Select(f => new AuthorFollowerProjection
                {
                    FollowerId = f.follower_id,
                    Username = f.follower.account.username,
                    AvatarUrl = f.follower.account.avatar_url,
                    NotificationsEnabled = f.noti_new_story ?? true,
                    FollowedAt = f.created_at
                });

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(f => f.FollowedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(IReadOnlyList<AuthorFollowingProjection> Items, int Total)> GetFollowingAsync(Guid readerId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.follows
                .AsNoTracking()
                .Where(f => f.follower_id == readerId)
                .Select(f => new AuthorFollowingProjection
                {
                    AuthorId = f.followee_id,
                    Username = f.followee.account.username,
                    AvatarUrl = f.followee.account.avatar_url,
                    NotificationsEnabled = f.noti_new_story ?? true,
                    FollowedAt = f.created_at
                });

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(f => f.FollowedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<IReadOnlyList<Guid>> GetFollowerIdsForNotificationsAsync(Guid authorId, CancellationToken ct = default)
        {
            return await _db.follows
                .AsNoTracking()
                .Where(f => f.followee_id == authorId && (f.noti_new_story ?? true))
                .Select(f => f.follower_id)
                .ToListAsync(ct);
        }
    }
}
