using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Notification;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Interfaces;

namespace Service.Services
{
    public class NotificationService : INotificationService
    {
        private static readonly JsonSerializerOptions PayloadSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationDispatcher _dispatcher;

        public NotificationService(INotificationRepository notificationRepository, INotificationDispatcher dispatcher)
        {
            _notificationRepository = notificationRepository;
            _dispatcher = dispatcher;
        }

        public async Task<NotificationResponse> CreateAsync(NotificationCreateModel model, CancellationToken ct = default)
        {
            var entity = new notification
            {
                notification_id = Guid.NewGuid(),
                recipient_id = model.RecipientId,
                type = model.Type,
                title = model.Title,
                message = model.Message,
                payload = model.Payload == null ? null : JsonSerializer.Serialize(model.Payload, PayloadSerializerOptions),
                is_read = false,
                created_at = TimezoneConverter.VietnamNow
            };

            await _notificationRepository.AddAsync(entity, ct);

            var response = Map(entity);
            await _dispatcher.DispatchAsync(response);
            return response;
        }

        public async Task<PagedResult<NotificationResponse>> GetAsync(Guid recipientId, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedPage = page <= 0 ? 1 : page;
            var normalizedSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var (items, total) = await _notificationRepository.GetAsync(recipientId, normalizedPage, normalizedSize, ct);
            var responses = new List<NotificationResponse>(items.Count);
            foreach (var entity in items)
            {
                responses.Add(Map(entity));
            }

            return new PagedResult<NotificationResponse>
            {
                Items = responses,
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default)
        {
            await _notificationRepository.MarkReadAsync(recipientId, notificationId, ct);
        }

        public async Task MarkAllReadAsync(Guid recipientId, CancellationToken ct = default)
        {
            await _notificationRepository.MarkAllReadAsync(recipientId, ct);
        }

        private static NotificationResponse Map(notification entity)
        {
            object? payload = null;
            if (!string.IsNullOrWhiteSpace(entity.payload))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<object>(entity.payload!, PayloadSerializerOptions);
                }
                catch
                {
                    payload = entity.payload;
                }
            }

            return new NotificationResponse
            {
                RecipientId = entity.recipient_id,
                NotificationId = entity.notification_id,
                Type = entity.type,
                Title = entity.title,
                Message = entity.message,
                Payload = payload,
                IsRead = entity.is_read,
                CreatedAt = entity.created_at
            };
        }
    }
}
