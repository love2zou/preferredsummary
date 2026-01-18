using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Video.Application.Processing
{
   public interface IVideoAnalysisQueue
    {
        ValueTask EnqueueAsync(int fileId, CancellationToken ct = default);
        ValueTask<int> DequeueAsync(CancellationToken ct);
    }

     /// <summary>
    /// 基于 Channel 的内存队列：存放 fileId
    /// </summary>
    public sealed class VideoAnalysisQueue : IVideoAnalysisQueue
    {
        private readonly Channel<int> _ch;

        public VideoAnalysisQueue(int capacity = 1000)
        {
            var opt = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _ch = Channel.CreateBounded<int>(opt);
        }

        public async ValueTask EnqueueAsync(int fileId, CancellationToken ct = default)
        {
            await _ch.Writer.WriteAsync(fileId, ct);
        }

        public async ValueTask<int> DequeueAsync(CancellationToken ct)
        {
            return await _ch.Reader.ReadAsync(ct);
        }
    }
}
