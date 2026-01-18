using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Video.Application.Processing;

namespace Preferred.Api.Services
{
    public sealed class VideoAnalysisWorker : BackgroundService
    {
        private readonly IVideoAnalysisQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VideoAnalysisWorker> _logger;

        public VideoAnalysisWorker(
            IVideoAnalysisQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<VideoAnalysisWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int fileId = await _queue.DequeueAsync(stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var detector = scope.ServiceProvider.GetRequiredService<SparkDetectionService>();

                    await detector.ProcessFileAsync(fileId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "VideoAnalysis file failed. fileId={FileId}", fileId);
                }
            }
        }
    }
}
