using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Voice;
using Repository.Entities;

namespace Service.Interfaces
{
    public interface IVoicePricingService
    {
        Task<int> GetPriceAsync(int charCount, CancellationToken ct = default);
        Task<int> GetGenerationCostAsync(int charCount, CancellationToken ct = default);
        Task<IReadOnlyList<VoicePricingRuleResponse>> GetAllRulesAsync(CancellationToken ct = default);

        // Admin
        Task<IReadOnlyList<voice_price_rule>> GetRawRulesAsync(CancellationToken ct = default);
        Task UpdateRuleAsync(UpdateVoicePriceRuleRequest request, CancellationToken ct = default);
    }
}
