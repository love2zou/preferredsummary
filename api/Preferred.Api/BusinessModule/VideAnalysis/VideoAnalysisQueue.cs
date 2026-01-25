using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Video.Application.Processing
{
    public interface IVideoAnalysisQueue
    {
        /// <summary>
        /// 入队：fileId
        /// </summary>
        ValueTask EnqueueAsync(int fileId, CancellationToken ct = default);

        /// <summary>
        /// 出队：阻塞等待下一个 fileId
        /// </summary>
        ValueTask<int> DequeueAsync(CancellationToken ct = default);
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

        public async ValueTask<int> DequeueAsync(CancellationToken ct = default)
        {
            return await _ch.Reader.ReadAsync(ct);
        }
    }
}