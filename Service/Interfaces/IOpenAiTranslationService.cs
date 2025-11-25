using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IOpenAiTranslationService
    {
        Task<string> TranslateAsync(string content, string sourceLanguageCode, string targetLanguageCode, CancellationToken ct = default);
    }
}
