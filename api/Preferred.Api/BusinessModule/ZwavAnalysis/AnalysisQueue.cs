using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Zwav.Application.Processing
{
    public interface IAnalysisQueue
    {
        ValueTask EnqueueAsync(string analysisGuid, CancellationToken ct = default);
        ValueTask<string> DequeueAsync(CancellationToken ct);
    }

    public class AnalysisQueue : IAnalysisQueue
    {
        private readonly Channel<string> _channel;

        public AnalysisQueue()
        {
            _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public ValueTask EnqueueAsync(string analysisGuid, CancellationToken ct = default)
            => _channel.Writer.WriteAsync(analysisGuid, ct);

        public ValueTask<string> DequeueAsync(CancellationToken ct)
            => _channel.Reader.ReadAsync(ct);
    }
}