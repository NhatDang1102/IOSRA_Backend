using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Service.Models;

namespace Service.Interfaces
{
    public interface IVoiceSynthesisQueue
    {
        ValueTask EnqueueAsync(VoiceSynthesisJob job, CancellationToken ct = default);

        IAsyncEnumerable<VoiceSynthesisJob> DequeueAsync(CancellationToken ct);
    }
}
