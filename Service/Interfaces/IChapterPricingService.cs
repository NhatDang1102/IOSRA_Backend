using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterPricingService
    {
        Task<int> GetPriceAsync(int wordCount, CancellationToken ct = default);
    }
}
