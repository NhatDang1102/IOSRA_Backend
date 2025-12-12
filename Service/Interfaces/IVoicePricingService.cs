using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;
using Contract.DTOs.Response.Voice;

namespace Service.Interfaces
{
    public interface IVoicePricingService
    {
        Task<int> GetPriceAsync(int charCount, CancellationToken ct = default);
        Task<int> GetGenerationCostAsync(int charCount, CancellationToken ct = default);
        Task<IReadOnlyList<VoicePricingRuleResponse>> GetAllRulesAsync(CancellationToken ct = default);
    }
}
