using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
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

        public AnalysisWorker(
            ILogger<AnalysisWorker> logger,
            IAnalysisQueue queue,
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
                ZwavAnalysisQueueItem item;

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
                    // 写端已完成（应用关闭或队列完成）
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dequeue failed.");
                    // 避免死循环打日志：短暂让出执行权
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                try
                {
                    await ProcessOneAsync(item, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 应用停机，直接退出
                    break;
                }
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
             // 1) 取任务（保留：用于状态更新、保存解析结果等）
            var analysis = await context.ZwavAnalyses
                .FirstOrDefaultAsync(x => x.AnalysisGuid == item.AnalysisGuid, ct);

            if (analysis == null) return;

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingRead, 5, null, DateTime.UtcNow, null, ct);

            // 3) 解压目录逻辑
            var baseDir = Path.GetDirectoryName(item.StoragePath);
            var extractDir = string.IsNullOrWhiteSpace(item.ExtractPath)
                ? Path.Combine(baseDir, $"extracted_{item.AnalysisGuid}") // 生成唯一目录名
                : item.ExtractPath;

            ZwavZipHelper.EnsureExtract(item.StoragePath, extractDir);

            // 4) 若原 ExtractPath 为空，需要回写 Tb_ZwavFile：用 Attach 更新，避免再查询
            if (string.IsNullOrWhiteSpace(item.ExtractPath))
            {
                var stub = new ZwavFile { Id = item.FileId, ExtractPath = extractDir, UpdTime = DateTime.UtcNow };
                context.ZwavFiles.Attach(stub);
                context.Entry(stub).Property(x => x.ExtractPath).IsModified = true;
                context.Entry(stub).Property(x => x.UpdTime).IsModified = true;
                await context.SaveChangesAsync(ct);
            }

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingExtract, 20, null, null, null, ct);

            // 4) 后续核心文件查找、CFG/HDR/DAT 解析照旧
            var (cfgPath, hdrPath, datPath) = ZwavZipHelper.FindCoreFiles(extractDir);
            if (string.IsNullOrWhiteSpace(cfgPath)) throw new Exception("CFG not found in archive.");
            if (string.IsNullOrWhiteSpace(datPath)) throw new Exception("DAT not found in archive.");

            // 5) 解析 CFG
            var cfgText = ZwavZipHelper.ReadTextWithFallbacks(cfgPath, "cfg");
            var cfg = _cfgParser.Parse(cfgText);

            // Upsert Tb_ZwavCfg
            await UpsertCfgAsync(context, analysis.Id, cfg, ct);

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingCfg, 45, null, null, null, ct);

            // 6) 解析 HDR（可选）
            if (!string.IsNullOrWhiteSpace(hdrPath))
            {
                var hdrText = ZwavZipHelper.ReadTextWithFallbacks(hdrPath, "hdr");
                var hdr = _hdrParser.Parse(hdrText);

                await UpsertHdrAsync(context, analysis.Id, hdr, ct);
            }

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingHdr, 65, null, null, null, ct);

            // 7) 通道落库（覆盖式：先删后插，使用事务）
            await ReplaceChannelsAsync(context, analysis.Id, cfg, ct);

            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingChannel, 80, null, null, null, ct);

            // 8) DAT 元信息
            byte[] datBuf = File.ReadAllBytes(datPath);
            var (recordSize, totalRecords, digitalWords) =
                DatMetaCalculator.InspectDat(datBuf, cfg.AnalogCount, cfg.DigitalCount);
            
            analysis.RecordSize = recordSize;
            analysis.TotalRecords = (int)Math.Min(totalRecords, int.MaxValue); // 你表是 INT，先保护；建议后面改 BIGINT
            analysis.DigitalWords = digitalWords;
            analysis.UpdTime = DateTime.UtcNow;

            int maxRows = 50000;       
            var waveResult = DatMetaCalculator.ParseDatAllChannels(datBuf, cfg, maxRows);
            await UpdateStatusAsync(context, analysis, ZwavConstants.ParsingDat, 90, null, null, null, ct);
            //DAT数据落库
            await UpsertWaveDataAsync(context, analysis.Id, waveResult, ct);
            // 9) 完成
            await UpdateStatusAsync(context, analysis, ZwavConstants.Completed, 100, null, null, DateTime.UtcNow, ct);
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
                entity = new ZwavCfg
                {
                    AnalysisId = analysisId,
                    CrtTime = now
                };
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
                entity = new ZwavHdr
                {
                    AnalysisId = analysisId,
                    CrtTime = now
                };
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

        private static readonly System.Reflection.PropertyInfo[] ChannelProps =
            Enumerable.Range(1, ZwavConstants.MaxAnalog) // 这里填你最大通道数
                .Select(i => typeof(ZwavData).GetProperty($"Channel{i}"))
                .ToArray();

          private static async Task UpsertWaveDataAsync(
            ApplicationDbContext context,
            int analysisId,
            WaveDataParseResult result,
            CancellationToken ct)
        {
            if (result?.Rows == null || result.Rows.Count == 0) return;

            var now = DateTime.UtcNow;

            // 1) 一次性取出本次涉及的 SampleNo
            var sampleNos = result.Rows.Select(r => r.SampleNo).Distinct().ToList();

            // 2) 一次性查询已有数据，构建字典（避免 N 次查询）
            var existing = await context.ZwavDatas
                .Where(x => x.AnalysisId == analysisId && sampleNos.Contains(x.SampleNo))
                .ToListAsync(ct);

            var map = existing.ToDictionary(x => x.SampleNo);

            // 3) 大批量写入优化：关闭 AutoDetectChanges + 分批提交
            var oldAuto = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                const int batchSize = 1000;
                int pending = 0;

                foreach (var row in result.Rows)
                {
                    if (!map.TryGetValue(row.SampleNo, out var entity))
                    {
                        entity = new ZwavData
                        {
                            AnalysisId = analysisId,
                            SampleNo = row.SampleNo,
                            CrtTime = now
                        };
                        context.ZwavDatas.Add(entity);
                        map[row.SampleNo] = entity;
                    }

                    entity.TimeRaw = row.TimeRaw;
                    entity.UpdTime = now;

                    // Channels：使用缓存的 PropertyInfo，避免重复 GetProperty
                    if (row.Channels != null)
                    {
                        var len = Math.Min(row.Channels.Length, ChannelProps.Length);
                        for (int i = 0; i < len; i++)
                        {
                            var p = ChannelProps[i];
                            if (p != null) p.SetValue(entity, row.Channels[i]);
                        }
                    }

                    // Digitals
                    if (row.DigitalWords != null)
                    {
                        entity.DigitalWords = row.DigitalWords;
                    }

                    pending++;
                    if (pending >= batchSize)
                    {
                        await context.SaveChangesAsync(ct);
                        pending = 0;

                        // 如果数据极大，且你不需要继续更新已跟踪实体，可以定期清理跟踪，降低内存
                        // 在 EF Core 5.0 以下版本没有 ChangeTracker.Clear()，可手动分离已跟踪实体
                        foreach (var entry in context.ChangeTracker.Entries<ZwavData>().ToList())
                        {
                            entry.State = EntityState.Detached;
                        }
                        // Clear 后 map 里的 entity 可能失效（变成 detached），如果你需要后续继续更新同一 SampleNo，
                        // 不建议 Clear；或改成批内 map。
                    }
                }

                if (pending > 0)
                    await context.SaveChangesAsync(ct);
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = oldAuto;
            }
        }

    }
}