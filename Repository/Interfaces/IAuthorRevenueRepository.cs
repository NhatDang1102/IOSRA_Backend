using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.DataModels;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAuthorRevenueRepository
    {
        Task<author?> GetAuthorAsync(Guid authorAccountId, CancellationToken ct = default);
        Task<(List<author_revenue_transaction> Items, int Total)> GetTransactionsAsync(Guid authorAccountId, int page, int pageSize, string? type, DateTime? from, DateTime? to, CancellationToken ct = default);
        
        Task<bool> IsStoryOwnedByAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default);
        Task<story?> GetStoryOwnedByAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default);
        Task<bool> IsChapterOwnedByAuthorAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);
        Task<chapter?> GetChapterOwnedByAuthorAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

        Task<(List<RevenuePurchaseLogData> Items, int Total, long TotalRevenue, long ChapterRevenue, long VoiceRevenue, int TotalChapterCount, int TotalVoiceCount)> GetStoryPurchaseLogsAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default);
        Task<(List<RevenuePurchaseLogData> Items, int Total, long TotalRevenue, long ChapterRevenue, long VoiceRevenue, int TotalChapterCount, int TotalVoiceCount)> GetChapterPurchaseLogsAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default);

        Task AddTransactionAsync(author_revenue_transaction transaction, CancellationToken ct = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
