using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Respond.Chapter;

namespace Service.Interfaces
{
    public interface IChapterPurchaseService
    {
        Task<ChapterPurchaseResponse> PurchaseAsync(Guid readerAccountId, Guid chapterId, CancellationToken ct = default);
    }
}
