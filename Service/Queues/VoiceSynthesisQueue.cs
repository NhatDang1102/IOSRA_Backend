using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Service.Interfaces;
using Service.Models;

namespace Service.Queues
{
    public class VoiceSynthesisQueue : IVoiceSynthesisQueue
    {
        private readonly Channel<VoiceSynthesisJob> _channel;

        public VoiceSynthesisQueue()
        {
            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<VoiceSynthesisJob>(options);
        }

        public ValueTask EnqueueAsync(VoiceSynthesisJob job, CancellationToken ct = default)
            => _channel.Writer.WriteAsync(job, ct);

        public IAsyncEnumerable<VoiceSynthesisJob> DequeueAsync(CancellationToken ct)
            => _channel.Reader.ReadAllAsync(ct);
    }
}
