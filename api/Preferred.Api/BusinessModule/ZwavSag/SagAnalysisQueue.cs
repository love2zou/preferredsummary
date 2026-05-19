using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Zwav.Application.Sag
{
    public sealed class ZwavSagAnalysisQueueItem
    {
        public int FileId { get; set; }

        public int AnalysisId { get; set; }

        public int ProcessingEventId { get; set; }

        public string OriginalName { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public AnalyzeZwavSagRequest Request { get; set; }
    }

    public interface IZwavSagAnalysisQueue
    {
        ValueTask EnqueueAsync(ZwavSagAnalysisQueueItem item, CancellationToken ct = default);

        ValueTask<ZwavSagAnalysisQueueItem> DequeueAsync(CancellationToken ct);

        void Complete(Exception error = null);
    }

    public sealed class ZwavSagAnalysisQueue : IZwavSagAnalysisQueue
    {
        private readonly Channel<ZwavSagAnalysisQueueItem> _channel;

        public ZwavSagAnalysisQueue()
        {
            _channel = Channel.CreateUnbounded<ZwavSagAnalysisQueueItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }

        public ValueTask EnqueueAsync(ZwavSagAnalysisQueueItem item, CancellationToken ct = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (_channel.Writer.TryWrite(item))
                return new ValueTask();

            return _channel.Writer.WriteAsync(item, ct);
        }

        public ValueTask<ZwavSagAnalysisQueueItem> DequeueAsync(CancellationToken ct)
        {
            return _channel.Reader.ReadAsync(ct);
        }

        public void Complete(Exception error = null)
        {
            _channel.Writer.TryComplete(error);
        }
    }
}
