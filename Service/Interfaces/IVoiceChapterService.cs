using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Voice;
using Contract.DTOs.Response.Voice;

namespace Service.Interfaces
{
    public interface IVoiceChapterService
    {
        Task<VoiceChapterStatusResponse> GetAsync(Guid requesterAccountId, Guid chapterId, CancellationToken ct = default);
        Task<VoiceChapterCharCountResponse> GetCharCountAsync(Guid authorAccountId, Guid chapterId, CancellationToken ct = default);
        Task<IReadOnlyList<VoicePresetResponse>> GetPresetsAsync(CancellationToken ct = default);
        Task<VoiceChapterOrderResponse> OrderVoicesAsync(Guid authorAccountId, Guid chapterId, VoiceChapterOrderRequest request, CancellationToken ct = default);
    }
}
