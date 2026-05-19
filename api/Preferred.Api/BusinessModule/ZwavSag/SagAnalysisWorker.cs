using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Preferred.Api.Services;
using Zwav.Application.Sag;

namespace Zwav.Application.Workers
{
    public class ZwavSagAnalysisWorker : BackgroundService
    {
        private readonly ILogger<ZwavSagAnalysisWorker> _logger;
        private readonly IZwavSagAnalysisQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;

        public ZwavSagAnalysisWorker(
            ILogger<ZwavSagAnalysisWorker> logger,
            IZwavSagAnalysisQueue queue,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ZwavSagAnalysisQueueItem item;

                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "暂降分析队列出队失败");
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IZwavSagEventService>();
                    await svc.ProcessQueuedAnalyzeAsync(item, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "暂降分析后台处理失败，FileId={FileId}, AnalysisId={AnalysisId}, ProcessingEventId={ProcessingEventId}",
                        item?.FileId,
                        item?.AnalysisId,
                        item?.ProcessingEventId);
                }
            }
        }
    }
}
