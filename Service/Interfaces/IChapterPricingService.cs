using Contract.DTOs.Request.Admin;
using Repository.Entities;

namespace Service.Interfaces
{
    public interface IChapterPricingService
    {
        Task<int> GetPriceAsync(int charCount, CancellationToken ct = default);
        
        // Admin
        Task<IReadOnlyList<chapter_price_rule>> GetAllRulesAsync(CancellationToken ct = default);
        Task UpdateRuleAsync(UpdateChapterPriceRuleRequest request, CancellationToken ct = default);
    }
}
