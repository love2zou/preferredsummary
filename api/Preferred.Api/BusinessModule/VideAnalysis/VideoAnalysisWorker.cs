using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Video.Application.Processing;
using Video.Application.Dto;

namespace Preferred.Api.Services
{
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
            using var gate = new SemaphoreSlim(dop, dop);

            var inFlight = new ConcurrentDictionary<int, Task>();

            _logger.LogInformation(
                "VideoAnalysisWorker started. DOP={Dop}, TimeoutSec={TimeoutSec}, MaxRetry={MaxRetry}",
                dop, _opt.PerFileTimeoutSec, _opt.MaxRetryCount);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    int fileId;

                    try
                    {
                        // 关键：先拿并发额度再 Dequeue，避免无限堆任务导致内存涨
                        await gate.WaitAsync(stoppingToken);
                        fileId = await _queue.DequeueAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        gate.Release();
                        _logger.LogError(ex, "VideoAnalysisWorker dequeue failed.");
                        await SafeDelay(_opt.DequeueErrorBackoffMs, stoppingToken);
                        continue;
                    }

                    var task = ProcessOneWithGuardsAsync(fileId, gate, stoppingToken);

                    inFlight[fileId] = task;

                    _ = task.ContinueWith(_ =>
                    {
                        inFlight.TryRemove(fileId, out _);
                    }, TaskScheduler.Default);
                }
            }
            finally
            {
                // 停机：尽力等待当前在跑任务结束
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
                    // ignore
                }

                _logger.LogInformation("VideoAnalysisWorker stopped.");
            }
        }

        private async Task ProcessOneWithGuardsAsync(int fileId, SemaphoreSlim gate, CancellationToken stoppingToken)
        {
            try
            {
                // 1) 执行（带超时 + stopToken 联动）
                bool ok = await ExecuteWithTimeoutAsync(fileId, stoppingToken);

                if (ok)
                {
                    // 成功：清掉重试计数
                    _retryCount.TryRemove(fileId, out _);
                    return;
                }

                // 2) 未成功（超时/失败）：决定是否重试
                await HandleRetryOrGiveUpAsync(fileId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 正常停机取消
            }
            catch (Exception ex)
            {
                // 兜底：极少数未被内部捕获的异常
                _logger.LogError(ex, "VideoAnalysis unexpected failure. fileId={FileId}", fileId);
                await SafeDelay(_opt.WorkErrorBackoffMs, stoppingToken);
                await HandleRetryOrGiveUpAsync(fileId, stoppingToken);
            }
            finally
            {
                try { gate.Release(); } catch { }
            }
        }

        /// <summary>
        /// 返回 true=成功；false=失败/超时（已记录日志）
        /// </summary>
        private async Task<bool> ExecuteWithTimeoutAsync(int fileId, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var detector = scope.ServiceProvider.GetRequiredService<SparkDetectionService>();

            // 如果不启用超时，直接跑
            if (_opt.PerFileTimeoutSec <= 0)
            {
                try
                {
                    await detector.ProcessFileAsync(fileId, stoppingToken);
                    return true;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "VideoAnalysis file failed. fileId={FileId}", fileId);
                    await SafeDelay(_opt.WorkErrorBackoffMs, stoppingToken);
                    return false;
                }
            }

            // 启用超时：创建 linked token（停机/超时任一触发都会取消）
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_opt.PerFileTimeoutSec));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

            try
            {
                await detector.ProcessFileAsync(fileId, linkedCts.Token);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 停机取消：交给上层 break
                throw;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // 超时：记日志，走重试策略
                _logger.LogError(
                    "VideoAnalysis file timeout after {TimeoutSec}s. fileId={FileId}",
                    _opt.PerFileTimeoutSec, fileId);

                await SafeDelay(_opt.WorkErrorBackoffMs, stoppingToken);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VideoAnalysis file failed. fileId={FileId}", fileId);
                await SafeDelay(_opt.WorkErrorBackoffMs, stoppingToken);
                return false;
            }
        }

        private async Task HandleRetryOrGiveUpAsync(int fileId, CancellationToken stoppingToken)
        {
            int cur = _retryCount.AddOrUpdate(fileId, 1, (_, old) => old + 1);

            // cur 表示“已经失败了多少次（含本次）”；重试次数上限是 MaxRetryCount（不含首次）：
            // - MaxRetryCount=0：首次失败就放弃
            // - MaxRetryCount=2：最多跑 1次 + 重试2次 = 3次
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

            await SafeDelay(delayMs, stoppingToken);

            // 重新入队（仍支持取消）
            await _queue.EnqueueAsync(fileId, stoppingToken);
        }

        private static int ComputeBackoffMs(int attempt, int baseMs, int maxMs, double jitterRatio)
        {
            if (baseMs <= 0) baseMs = 100;
            if (maxMs <= 0) maxMs = 5000;
            if (jitterRatio < 0) jitterRatio = 0;
            if (jitterRatio > 1) jitterRatio = 1;

            // attempt=1 -> base
            // attempt=2 -> base*2
            // attempt=3 -> base*4 ...
            double raw = baseMs * Math.Pow(2, Math.Max(0, attempt - 1));
            if (raw > maxMs) raw = maxMs;

            // jitter: ± jitterRatio
            double jitter = 1.0;
            if (jitterRatio > 0)
            {
                var rnd = new Random().NextDouble(); // 0~1
                double span = jitterRatio * 2.0;      // 0~2*jitterRatio
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

            int cpu = Environment.ProcessorCount;
            int dop = Math.Max(1, Math.Min(cpu / 2, 4));
            return dop;
        }

        private static async Task SafeDelay(int ms, CancellationToken ct)
        {
            if (ms <= 0) return;
            try { await Task.Delay(ms, ct); } catch { }
        }
    }
}