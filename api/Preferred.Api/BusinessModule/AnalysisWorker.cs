using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
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
                var guid = await _queue.DequeueAsync(stoppingToken);

                try
                {
                    await ProcessOneAsync(guid, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Analysis failed. guid={guid}", guid);

                    // 失败状态落库
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var a = await context.ZwavAnalyses.FirstOrDefaultAsync(x => x.AnalysisGuid == guid, stoppingToken);
                    if (a != null)
                    {
                        a.Status = "Failed";
                        a.Progress = 100;
                        a.ErrorMessage = ex.Message;
                        a.FinishTime = DateTime.UtcNow;
                        a.UpdTime = DateTime.UtcNow;

                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
        }

        private async Task ProcessOneAsync(string analysisGuid, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1) 取任务
            var analysis = await context.ZwavAnalyses
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);

            if (analysis == null) return;

            // 标记 Parsing
            await UpdateStatusAsync(context, analysis, "Parsing", 5, null, startTime: DateTime.UtcNow, finishTime: null, ct);

            // 2) 取文件
            var file = await context.ZwavFiles.FirstOrDefaultAsync(x => x.Id == analysis.FileId, ct);
            if (file == null) throw new Exception("File not found.");

            // 3) 解压
            var baseDir = Path.GetDirectoryName(file.StoragePath);
            var extractDir = string.IsNullOrWhiteSpace(file.ExtractPath)
                ? Path.Combine(baseDir, "extracted")
                : file.ExtractPath;

            ZwavZipHelper.EnsureExtract(file.StoragePath, extractDir);

            // 若原表 ExtractPath 为空，可回写一次，便于后续复用/排查
            if (string.IsNullOrWhiteSpace(file.ExtractPath))
            {
                file.ExtractPath = extractDir;
                file.UpdTime = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
            }

            await UpdateStatusAsync(context, analysis, "Parsing", 20, null, null, null, ct);

            // 4) 找核心文件
            var (cfgPath, hdrPath, datPath) = ZwavZipHelper.FindCoreFiles(extractDir);
            if (string.IsNullOrWhiteSpace(cfgPath)) throw new Exception("CFG not found in archive.");
            if (string.IsNullOrWhiteSpace(datPath)) throw new Exception("DAT not found in archive.");

            // 5) 解析 CFG
            var cfgText = ZwavZipHelper.ReadTextWithFallbacks(cfgPath, "cfg");
            var cfg = _cfgParser.Parse(cfgText);

            // Upsert Tb_ZwavCfg
            await UpsertCfgAsync(context, analysis.Id, cfg, ct);

            await UpdateStatusAsync(context, analysis, "Parsing", 45, null, null, null, ct);

            // 6) 解析 HDR（可选）
            if (!string.IsNullOrWhiteSpace(hdrPath))
            {
                var hdrText = ZwavZipHelper.ReadTextWithFallbacks(hdrPath, "hdr");
                var hdr = _hdrParser.Parse(hdrText);

                await UpsertHdrAsync(context, analysis.Id, hdr, ct);
            }

            await UpdateStatusAsync(context, analysis, "Parsing", 65, null, null, null, ct);

            // 7) 通道落库（覆盖式：先删后插，使用事务）
            await ReplaceChannelsAsync(context, analysis.Id, cfg, ct);

            await UpdateStatusAsync(context, analysis, "Parsing", 80, null, null, null, ct);

            // 8) DAT 元信息
            byte[] datBuf = File.ReadAllBytes(datPath);
            var (recordSize, totalRecords, digitalWords) =
                DatMetaCalculator.InspectDat(datBuf, cfg.AnalogCount, cfg.DigitalCount);
            
            analysis.RecordSize = recordSize;
            analysis.TotalRecords = (int)Math.Min(totalRecords, int.MaxValue); // 你表是 INT，先保护；建议后面改 BIGINT
            analysis.DigitalWords = digitalWords;
            analysis.UpdTime = DateTime.UtcNow;

            await context.SaveChangesAsync(ct);

            int maxRows = 50000;
            var rows = DatMetaCalculator.ParseDatAllChannels(datBuf, cfg, maxRows, true);
            await UpsertWaveDataAsync(context, analysis.Id, rows, ct);
            // 9) 完成
            await UpdateStatusAsync(context, analysis, "Ready", 100, null, null, DateTime.UtcNow, ct);
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

        private static async Task UpsertWaveDataAsync_OLD(ApplicationDbContext context, int analysisId, WaveDataParseResult result, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            // 遍历解析后的每一行数据并插入到数据库
            foreach (var row in result.Rows)
            {
                // 尝试查找是否已有该样本号的记录
                var entity = await context.ZwavDatas
                    .FirstOrDefaultAsync(x => x.AnalysisId == analysisId && x.SampleNo == row.SampleNo, ct);

                if (entity == null)
                {
                    // 如果没有找到，创建新实体
                    entity = new ZwavData
                    {
                        AnalysisId = analysisId,
                        SampleNo = row.SampleNo,
                        TimeRaw = row.TimeRaw,
                        CrtTime = now
                    };
                    context.ZwavDatas.Add(entity);
                }

                // 更新或设置模拟量通道（示例只更新前几个，您可以根据需要继续扩展）
                for (int i = 0; i < row.Channels.Length; i++)
                {
                    var channelProperty = typeof(ZwavData).GetProperty($"Channel{i + 1}");
                    if (channelProperty != null)
                    {
                        channelProperty.SetValue(entity, row.Channels[i]);
                    }
                }

                // 更新数字量通道（示例只更新前几个，您可以根据需要继续扩展）
                if (row.Digitals != null)
                {
                    for (int i = 0; i < row.Digitals.Length; i++)
                    {
                        var digitalProperty = typeof(ZwavData).GetProperty($"Digital{i + 1}");
                        if (digitalProperty != null)
                        {
                            digitalProperty.SetValue(entity, row.Digitals[i]);
                        }
                    }
                }

                // 设置其他字段（如时间）
                entity.UpdTime = now;
            }
            // 保存更改
            await context.SaveChangesAsync(ct);
        }

        private static readonly System.Reflection.PropertyInfo[] ChannelProps =
            Enumerable.Range(1, 70) // 这里填你最大通道数
                .Select(i => typeof(ZwavData).GetProperty($"Channel{i}"))
                .ToArray();

        private static readonly System.Reflection.PropertyInfo[] DigitalProps =
            Enumerable.Range(1, 50) // 这里填你最大数字量通道数
                .Select(i => typeof(ZwavData).GetProperty($"Digital{i}"))
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
                    if (row.Digitals != null)
                    {
                        var len = Math.Min(row.Digitals.Length, DigitalProps.Length);
                        for (int i = 0; i < len; i++)
                        {
                            var p = DigitalProps[i];
                            if (p != null) p.SetValue(entity, row.Digitals[i]);
                        }
                    }

                    pending++;
                    if (pending >= batchSize)
                    {
                        await context.SaveChangesAsync(ct);
                        pending = 0;

                        // 如果数据极大，且你不需要继续更新已跟踪实体，可以定期清理跟踪，降低内存
                        // 在 EF Core 5.0 以下版本没有 ChangeTracker.Clear()，可手动分离已跟踪实体
                        foreach (var entry in context.ChangeTracker.Entries().ToList())
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