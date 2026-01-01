using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Zwav.Application.Processing
{
    public interface IAnalysisQueue
    {
        ValueTask EnqueueAsync(ZwavAnalysisQueueItem item, CancellationToken ct = default);
        bool TryEnqueue(ZwavAnalysisQueueItem item);
        ValueTask<ZwavAnalysisQueueItem> DequeueAsync(CancellationToken ct);
        void Complete(Exception error = null);
    }

    /// <summary>
    /// 进程内解析队列：基于 Channel，单消费者（Worker）读取，多生产者（Controller/AppService）写入。
    /// </summary>
    public sealed class AnalysisQueue : IAnalysisQueue
    {
        private readonly Channel<ZwavAnalysisQueueItem> _channel;

        /// <summary>
        /// 默认使用无界队列。若担心内存膨胀，可切换为有界（见下方注释）。
        /// </summary>
        public AnalysisQueue()
        {
            _channel = Channel.CreateUnbounded<ZwavAnalysisQueueItem>(new UnboundedChannelOptions
            {
                SingleReader = true,   // 通常一个后台 worker 消费
                SingleWriter = false,  // 多个请求线程可能同时入队
                AllowSynchronousContinuations = false
            });

            // 如果你希望“限流/避免内存堆积”，建议用有界队列：
            // _channel = Channel.CreateBounded<ZwavAnalysisQueueItem>(new BoundedChannelOptions(capacity: 2000)
            // {
            //     SingleReader = true,
            //     SingleWriter = false,
            //     FullMode = BoundedChannelFullMode.Wait, // 满了就等待（也可 DropOldest/DropWrite）
            //     AllowSynchronousContinuations = false
            // });
        }

        /// <summary>
        /// 入队（支持取消）。优先走 TryWrite 快路径，避免不必要的异步状态机开销。
        /// </summary>
        public ValueTask EnqueueAsync(ZwavAnalysisQueueItem item, CancellationToken ct = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // 快路径：队列未满且写端可写时，TryWrite 直接成功
            if (_channel.Writer.TryWrite(item))
                return new ValueTask();
                
            // 慢路径：进入异步等待（有界队列满、或读写竞争时）
            return _channel.Writer.WriteAsync(item, ct);
        }

        /// <summary>
        /// 尝试入队：不阻塞、不抛取消异常，适合“尽力而为”的场景。
        /// </summary>
        public bool TryEnqueue(ZwavAnalysisQueueItem item)
        {
            if (item == null) return false;
            return _channel.Writer.TryWrite(item);
        }

        /// <summary>
        /// 出队：单消费者 await ReadAsync。
        /// </summary>
        public ValueTask<ZwavAnalysisQueueItem> DequeueAsync(CancellationToken ct)
            => _channel.Reader.ReadAsync(ct);

        /// <summary>
        /// 完成写端：应用关闭时通知消费者退出。
        /// </summary>
        public void Complete(Exception error = null)
            => _channel.Writer.TryComplete(error);
    }
}
