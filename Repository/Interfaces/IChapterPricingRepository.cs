using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IChapterPricingRepository
    {
        Task<IReadOnlyList<chapter_price_rule>> GetRulesAsync(CancellationToken ct = default);
    }
}
