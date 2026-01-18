using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Zwav.Application.Parsing;
using Zwav.Application.Processing;

namespace Zwav.Application.Workers
{
    public class AnalysisWorker : BackgroundService
    {
        private readonly ILogger<AnalysisWorker> _logger;
        private readonly IAnalysisQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly CfgParser _cfgParser = new CfgParser();
        private readonly HdrParser _hdrParser = new HdrParser();

        // 反射缓存：Channel1..Channel70
        private static readonly System.Reflection.PropertyInfo[] ChannelProps =
            Enumerable.Range(1, ZwavConstants.MaxAnalog) // MaxAnalog=70
                .Select(i => typeof(ZwavData).GetProperty($"Channel{i}"))
                .ToArray();

        public AnalysisWorker(ILogger<AnalysisWorker> logger, IAnalysisQueue queue, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ZwavAnalysisQueueItem item;

                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (ChannelClosedException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dequeue failed.");
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                try
                {
                    await ProcessOneAsync(item, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Analysis failed. guid={guid}, fileId={fileId}", item?.AnalysisGuid, item?.FileId);
                    await MarkFailedAsync(item?.AnalysisGuid, ex, stoppingToken);
                }
            }
        }

        private async Task MarkFailedAsync(string analysisGuid, Exception ex, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(analysisGuid)) return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var a = await context.ZwavAnalyses.FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return;

            a.Status = ZwavConstants.Failed;
            a.Progress = 100;
            a.ErrorMessage = ex.Message;
            a.FinishTime = DateTime.UtcNow;
            a.UpdTime = DateTime.UtcNow;

            await context.SaveChangesAsync(ct);
        }

        private async Task ProcessOneAsync(ZwavAnalysisQueueItem item, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var analysis = await context.ZwavAnalyses.FirstOrDefaultAsync(x => x.AnalysisGuid == item.AnalysisGuid, ct);
            if (analysis == null) return;

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingRead, 5, null, DateTime.UtcNow, null, ct);

            var baseDir = Path.GetDirectoryName(item.StoragePath);
            var extractDir = string.IsNullOrWhiteSpace(item.ExtractPath)
                ? Path.Combine(baseDir, $"extracted_{item.AnalysisGuid}")
                : item.ExtractPath;

            ZwavZipHelper.EnsureExtract(item.StoragePath, extractDir);

            if (string.IsNullOrWhiteSpace(item.ExtractPath))
            {
                var stub = new ZwavFile { Id = item.FileId, ExtractPath = extractDir, UpdTime = DateTime.UtcNow };
                context.ZwavFiles.Attach(stub);
                context.Entry(stub).Property(x => x.ExtractPath).IsModified = true;
                context.Entry(stub).Property(x => x.UpdTime).IsModified = true;
                await context.SaveChangesAsync(ct);
            }

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingExtract, 20, null, null, null, ct);

            var (cfgPath, hdrPath, datPath) = ZwavZipHelper.FindCoreFiles(extractDir);
            if (string.IsNullOrWhiteSpace(cfgPath)) throw new Exception("CFG not found in archive.");
            if (string.IsNullOrWhiteSpace(datPath)) throw new Exception("DAT not found in archive.");

            // CFG
            var cfgText = ZwavZipHelper.ReadTextWithFallbacks(cfgPath, "cfg");
            var cfg = _cfgParser.Parse(cfgText);
            await UpsertCfgAsync(context, analysis.Id, cfg, ct);
            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingCfg, 45, null, null, null, ct);

            // HDR
            if (!string.IsNullOrWhiteSpace(hdrPath))
            {
                var hdrText = ZwavZipHelper.ReadTextWithFallbacks(hdrPath, "hdr");
                var hdr = _hdrParser.Parse(hdrText);
                await UpsertHdrAsync(context, analysis.Id, hdr, ct);
            }
            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingHdr, 65, null, null, null, ct);

            // Channels
            await ReplaceChannelsAsync(context, analysis.Id, cfg, ct);
            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingChannel, 80, null, null, null, ct);

            // DAT 全量入库
            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingDat, 90, null, null, null, ct);

            await ImportDatAllAsync(datPath, cfg, analysis.Id, ct);

            await UpdateStatusAsync(context, analysis, ZwavConstants.Completed, 100, null, null, DateTime.UtcNow, ct);
        }

        private async Task ImportDatAllAsync(string datPath, CfgParseResult cfg, int analysisId, CancellationToken ct)
        {
            // 建议做成配置
            const int batchSize = 5000;
            const int progressEveryBatches = 2;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 覆盖式导入：先删旧数据（大任务建议用 ExecuteDeleteAsync）
            var old = context.ZwavDatas.Where(x => x.AnalysisId == analysisId);
            context.ZwavDatas.RemoveRange(old);
            await context.SaveChangesAsync(ct);

            var oldAutoDetect = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            long imported = 0;
            int batchIndex = 0;
            bool metaWritten = false;

            try
            {
                await using var fs = new FileStream(datPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: false);

                DatMetaCalculator.ParseDatAllChannelsStream(fs, cfg, batchSize, batch =>
                {
                    ct.ThrowIfCancellationRequested();

                    // 第一次批次写回元信息（只写一次）
                    if (!metaWritten)
                    {
                        WriteAnalysisMetaSync(analysisId, batch.RecordSize, batch.TotalRecords, batch.DigitalWords);
                        metaWritten = true;
                    }

                    var now = DateTime.UtcNow;
                    var entities = new List<ZwavData>(batch.BatchCount);

                    foreach (var r in batch.Rows)
                    {
                        var e = new ZwavData
                        {
                            AnalysisId = analysisId,
                            SampleNo = r.SampleNo,
                            TimeRaw = r.TimeRaw,
                            TimeMs = r.TimeMs,
                            DigitalWords = r.DigitalWords,
                            SeqNo = 0,
                            CrtTime = now,
                            UpdTime = now
                        };

                        if (r.Channels != null)
                        {
                            int len = Math.Min(r.Channels.Length, ChannelProps.Length);
                            for (int i = 0; i < len; i++)
                                ChannelProps[i]?.SetValue(e, r.Channels[i]);
                        }

                        entities.Add(e);
                    }

                    context.ZwavDatas.AddRange(entities);
                    context.SaveChanges();
                    context.ChangeTracker.Entries<ZwavData>().ToList().ForEach(e => e.State = EntityState.Detached);

                    imported += batch.BatchCount;
                    batchIndex++;

                    if (batchIndex % progressEveryBatches == 0 || imported == batch.TotalRecords)
                    {
                        int pct = 90 + (int)Math.Min(9, imported * 9.0 / Math.Max(1, batch.TotalRecords));
                        UpdateProgressSync(analysisId, pct);
                    }
                });

                // 导入结束：这里不写 100，外层 Completed 会写 100
                UpdateProgressSync(analysisId, 99);
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = oldAutoDetect;
            }
        }

        private void WriteAnalysisMetaSync(int analysisId, int recordSize, long totalRecords, int digitalWords)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stub = new ZwavAnalysis
            {
                Id = analysisId,
                RecordSize = recordSize,
                DigitalWords = digitalWords,
                TotalRecords = totalRecords > int.MaxValue ? int.MaxValue : (int)totalRecords, // 兼容你当前表结构
                UpdTime = DateTime.UtcNow
            };

            context.ZwavAnalyses.Attach(stub);
            context.Entry(stub).Property(x => x.RecordSize).IsModified = true;
            context.Entry(stub).Property(x => x.DigitalWords).IsModified = true;
            context.Entry(stub).Property(x => x.TotalRecords).IsModified = true;
            context.Entry(stub).Property(x => x.UpdTime).IsModified = true;

            context.SaveChanges();
        }

        private void UpdateProgressSync(int analysisId, int pct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stub = new ZwavAnalysis
            {
                Id = analysisId,
                Progress = pct,
                UpdTime = DateTime.UtcNow
            };

            context.ZwavAnalyses.Attach(stub);
            context.Entry(stub).Property(x => x.Progress).IsModified = true;
            context.Entry(stub).Property(x => x.UpdTime).IsModified = true;

            context.SaveChanges();
        }

        private static async Task UpdateStatusAsync(
            ApplicationDbContext context,
            ZwavAnalysis analysis,
            string status,
            int progress,
            string errorMessage,
            DateTime? startTime,
            DateTime? finishTime,
            CancellationToken ct)
        {
            analysis.Status = status;
            analysis.Progress = progress;
            analysis.ErrorMessage = errorMessage;

            if (startTime.HasValue && !analysis.StartTime.HasValue)
                analysis.StartTime = startTime;

            if (finishTime.HasValue)
                analysis.FinishTime = finishTime;

            analysis.UpdTime = DateTime.UtcNow;

            await context.SaveChangesAsync(ct);
        }

        private static async Task UpsertCfgAsync(ApplicationDbContext context, int analysisId, CfgParseResult cfg, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var entity = await context.ZwavCfgs.FirstOrDefaultAsync(x => x.AnalysisId == analysisId, ct);
            if (entity == null)
            {
                entity = new ZwavCfg { AnalysisId = analysisId, CrtTime = now };
                context.ZwavCfgs.Add(entity);
            }

            entity.FullCfgText = cfg.FullText;
            entity.StationName = cfg.StationName;
            entity.DeviceId = cfg.DeviceId;
            entity.Revision = cfg.Revision;
            entity.AnalogCount = cfg.AnalogCount;
            entity.DigitalCount = cfg.DigitalCount;
            entity.FrequencyHz = cfg.FrequencyHz;
            entity.TimeMul = cfg.TimeMul;
            entity.StartTimeRaw = cfg.StartTimeRaw;
            entity.TriggerTimeRaw = cfg.TriggerTimeRaw;
            entity.FormatType = cfg.FormatType;
            entity.DataType = cfg.DataType;
            entity.SampleRateJson = cfg.SampleRateJson;
            entity.SeqNo = 0;
            entity.UpdTime = now;

            await context.SaveChangesAsync(ct);
        }

        private static async Task UpsertHdrAsync(ApplicationDbContext context, int analysisId, HdrParseResult hdr, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var entity = await context.ZwavHdrs.FirstOrDefaultAsync(x => x.AnalysisId == analysisId, ct);
            if (entity == null)
            {
                entity = new ZwavHdr { AnalysisId = analysisId, CrtTime = now };
                context.ZwavHdrs.Add(entity);
            }

            entity.FaultStartTime = hdr.FaultStartTime;
            entity.FaultKeepingTime = hdr.FaultKeepingTime;
            entity.TripInfoJSON = hdr.TripInfoJSON;
            entity.DeviceInfoJson = hdr.DeviceInfoJson;
            entity.FaultInfoJson = hdr.FaultInfoJson;
            entity.DigitalStatusJson = hdr.DigitalStatusJson;
            entity.DigitalEventJson = hdr.DigitalEventJson;
            entity.SettingValueJson = hdr.SettingValueJson;
            entity.RelayEnaValueJSON = hdr.RelayEnaValueJSON;

            entity.SeqNo = 0;
            entity.UpdTime = now;

            await context.SaveChangesAsync(ct);
        }

        private static async Task ReplaceChannelsAsync(ApplicationDbContext context, int analysisId, CfgParseResult cfg, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            await using var tx = await context.Database.BeginTransactionAsync(ct);

            var old = context.ZwavChannels.Where(x => x.AnalysisId == analysisId);
            context.ZwavChannels.RemoveRange(old);
            await context.SaveChangesAsync(ct);

            var list = cfg.Channels.Select(c => new ZwavChannel
            {
                AnalysisId = analysisId,
                ChannelIndex = c.ChannelIndex,
                ChannelType = c.ChannelType,
                ChannelCode = c.Code,
                ChannelName = c.Name,
                Phase = c.Phase,
                Unit = c.Unit,
                RatioA = c.A,
                OffsetB = c.B,
                Skew = c.Skew,
                IsEnable = 1,
                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            }).ToList();

            if (list.Count > 0)
            {
                context.ZwavChannels.AddRange(list);
                await context.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
    }
}