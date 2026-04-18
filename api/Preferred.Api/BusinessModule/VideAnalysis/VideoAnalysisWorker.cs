using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Video.Application.Dto;
using Video.Application.Processing;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 视频分析后台消费者。
    /// 这个 Worker 只负责调度：从内存队列取出待分析的 fileId，按照配置的并发数启动分析任务，
    /// 并在单个文件失败时执行有限次数的退避重试。
    ///
    /// 注意：BackgroundService 要求 ExecuteAsync 保留取消标记参数，否则无法 override。
    /// 业务链路已经不再向队列、检测服务、延迟等待传递取消标记，避免分析过程中被取消打断。
    /// </summary>
    public sealed class VideoAnalysisWorker : BackgroundService
    {
        private readonly IVideoAnalysisQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VideoAnalysisWorker> _logger;
        private readonly VideoAnalysisWorkerOptions _opt;

        // 记录重试次数（按 fileId 维度）；同一进程生命周期有效
        private readonly ConcurrentDictionary<int, int> _retryCount = new ConcurrentDictionary<int, int>();

        public VideoAnalysisWorker(
            IVideoAnalysisQueue queue,
            IServiceScopeFactory scopeFactory,
            IOptions<VideoAnalysisWorkerOptions> opt,
            ILogger<VideoAnalysisWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _opt = opt?.Value ?? new VideoAnalysisWorkerOptions();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int dop = ResolveDop(_opt.MaxDegreeOfParallelism);

            // gate 是并发阀门：队列里可以堆很多 fileId，但同一时间真正跑 OpenCV 分析的任务数受 dop 限制。
            using var gate = new SemaphoreSlim(dop, dop);

            // 记录当前正在执行的任务，主要用于日志观察和 Worker 退出时尽量等待已有任务收尾。
            var inFlight = new ConcurrentDictionary<int, Task>();

            _logger.LogInformation(
                "VideoAnalysisWorker started. DOP={Dop}, MaxRetry={MaxRetry}",
                dop, _opt.MaxRetryCount);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    int fileId;
                    bool gateTaken = false;

                    try
                    {
                        // 关键：先拿并发额度再 Dequeue，避免无限堆任务导致内存涨。
                        // 这里不再传递取消标记，保证已经进入分析链路的文件不会被取消机制打断。
                        await gate.WaitAsync();
                        gateTaken = true;
                        fileId = await _queue.DequeueAsync();
                    }
                    catch (Exception ex)
                    {
                        if (gateTaken) TryRelease(gate);
                        _logger.LogError(ex, "VideoAnalysisWorker dequeue failed.");
                        await SafeDelay(_opt.DequeueErrorBackoffMs);
                        continue;
                    }

                    var task = ProcessOneWithGuardsAsync(fileId, gate);

                    inFlight[fileId] = task;

                    _ = task.ContinueWith(_ =>
                    {
                        inFlight.TryRemove(fileId, out _);
                    }, TaskScheduler.Default);
                }
            }
            finally
            {
                // 停机：尽力等待当前在跑任务结束。
                try
                {
                    var tasks = inFlight.Values;
                    if (tasks.Count > 0)
                    {
                        _logger.LogInformation("VideoAnalysisWorker stopping. Waiting in-flight tasks: {Count}", tasks.Count);
                        await Task.WhenAll(tasks);
                    }
                }
                catch
                {
                    // 退出阶段不再抛出异常，避免影响宿主进程关闭。
                }

                _logger.LogInformation("VideoAnalysisWorker stopped.");
            }
        }

        private async Task ProcessOneWithGuardsAsync(int fileId, SemaphoreSlim gate)
        {
            try
            {
                // 单个文件独立创建 DI Scope，确保 DbContext 和检测服务的生命周期跟本次分析绑定。
                bool ok = await ExecuteFileAsync(fileId);

                if (ok)
                {
                    // 成功后清理重试状态，避免后续手动重跑同一个 fileId 时沿用旧失败次数。
                    _retryCount.TryRemove(fileId, out _);
                    return;
                }

                await HandleRetryOrGiveUpAsync(fileId);
            }
            catch (Exception ex)
            {
                // 兜底：理论上 ExecuteFileAsync 已经吞掉单文件异常并返回 false，这里防止极端异常漏出导致任务静默结束。
                _logger.LogError(ex, "VideoAnalysis unexpected failure. fileId={FileId}", fileId);
                await SafeDelay(_opt.WorkErrorBackoffMs);
                await HandleRetryOrGiveUpAsync(fileId);
            }
            finally
            {
                TryRelease(gate);
            }
        }

        /// <summary>
        /// 执行单个文件分析。
        /// 返回 true 表示 SparkDetectionService 已完整处理并把文件状态写成成功；
        /// 返回 false 表示分析抛出异常，交给外层重试策略决定是否重新入队。
        /// </summary>
        private async Task<bool> ExecuteFileAsync(int fileId)
        {
            using var scope = _scopeFactory.CreateScope();
            var detector = scope.ServiceProvider.GetRequiredService<SparkDetectionService>();

            try
            {
                await detector.ProcessFileAsync(fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VideoAnalysis file failed. fileId={FileId}", fileId);
                await SafeDelay(_opt.WorkErrorBackoffMs);
                return false;
            }
        }

        private async Task HandleRetryOrGiveUpAsync(int fileId)
        {
            int cur = _retryCount.AddOrUpdate(fileId, 1, (_, old) => old + 1);

            // cur 表示已经失败了多少次（含本次）；MaxRetryCount 表示失败后最多再排队重试多少次。
            // 例如 MaxRetryCount=0：首次失败就放弃；MaxRetryCount=2：首次 + 2 次重试，共最多 3 次执行。
            if (cur > Math.Max(0, _opt.MaxRetryCount))
            {
                _logger.LogError(
                    "VideoAnalysis give up after retries. fileId={FileId}, failedCount={FailedCount}, maxRetry={MaxRetry}",
                    fileId, cur, _opt.MaxRetryCount);

                _retryCount.TryRemove(fileId, out _);
                return;
            }

            int delayMs = ComputeBackoffMs(cur, _opt.RetryBaseDelayMs, _opt.RetryMaxDelayMs, _opt.RetryJitterRatio);
            _logger.LogWarning(
                "VideoAnalysis retry scheduled. fileId={FileId}, attempt={Attempt}, delayMs={DelayMs}",
                fileId, cur, delayMs);

            await SafeDelay(delayMs);

            // 重新入队后会被同一个 Worker 循环再次消费；失败状态由 SparkDetectionService 写入数据库。
            await _queue.EnqueueAsync(fileId);
        }

        private static int ComputeBackoffMs(int attempt, int baseMs, int maxMs, double jitterRatio)
        {
            if (baseMs <= 0) baseMs = 100;
            if (maxMs <= 0) maxMs = 5000;
            if (jitterRatio < 0) jitterRatio = 0;
            if (jitterRatio > 1) jitterRatio = 1;

            // 指数退避：第 1 次失败等 base，第 2 次等 base*2，第 3 次等 base*4，之后不超过 max。
            double raw = baseMs * Math.Pow(2, Math.Max(0, attempt - 1));
            if (raw > maxMs) raw = maxMs;

            // 加 jitter 可以错开多个失败文件的重试时间，避免所有任务在同一毫秒重新冲进队列。
            double jitter = 1.0;
            if (jitterRatio > 0)
            {
                var rnd = new Random().NextDouble();
                double span = jitterRatio * 2.0;
                jitter = 1.0 - jitterRatio + rnd * span;
            }

            int ms = (int)Math.Round(raw * jitter);
            if (ms < 0) ms = 0;
            if (ms > maxMs) ms = maxMs;
            return ms;
        }

        private static int ResolveDop(int configured)
        {
            if (configured > 0) return configured;

            // 默认使用半数 CPU，上限 4，避免 OpenCV/IO/数据库同时被过多视频任务打满。
            int cpu = Environment.ProcessorCount;
            int dop = Math.Max(1, Math.Min(cpu / 2, 4));
            return dop;
        }

        private static async Task SafeDelay(int ms)
        {
            if (ms <= 0) return;

            try
            {
                await Task.Delay(ms);
            }
            catch
            {
                // 延迟只是退避手段，失败不影响后续重试/放弃逻辑。
            }
        }

        private static void TryRelease(SemaphoreSlim gate)
        {
            try
            {
                gate.Release();
            }
            catch
            {
                // gate 释放失败只可能发生在异常路径重复释放，忽略即可，避免覆盖真实业务异常。
            }
        }
    }
}
