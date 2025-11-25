using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IVoicePricingRepository
    {
        Task<IReadOnlyList<voice_price_rule>> GetRulesAsync(CancellationToken ct = default);
    }
}
