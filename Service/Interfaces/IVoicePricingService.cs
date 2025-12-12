using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IVoicePricingService
    {
        Task<int> GetPriceAsync(int charCount, CancellationToken ct = default);
        Task<int> GetGenerationCostAsync(int charCount, CancellationToken ct = default);
    }
}
