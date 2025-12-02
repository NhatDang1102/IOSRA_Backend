using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly AppDbContext _db;

        public NotificationRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<notification> AddAsync(notification entity, CancellationToken ct = default)
        {
            _db.notification.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<(IReadOnlyList<notification> Items, int Total)> GetAsync(Guid recipientId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.notification
                .AsNoTracking()
                .Where(n => n.recipient_id == recipientId);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(n => n.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<int> MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default)
        {
            var entity = await _db.notification
                .FirstOrDefaultAsync(n => n.notification_id == notificationId && n.recipient_id == recipientId, ct);

            if (entity == null || entity.is_read)
            {
                return 0;
            }

            entity.is_read = true;
            _db.notification.Update(entity);
            return await _db.SaveChangesAsync(ct);
        }

        public async Task<int> MarkAllReadAsync(Guid recipientId, CancellationToken ct = default)
        {
            var query = _db.notification.Where(n => n.recipient_id == recipientId && !n.is_read);
            var affected = await query.ExecuteUpdateAsync(setters => setters.SetProperty(n => n.is_read, true), ct);
            return affected;
        }
    }
}
