using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Zwav.Application.Parsing;
using Zwav.Application.Sag;

namespace Preferred.Api.Services
{
    public class ZwavSagEventService : IZwavSagEventService
    {
        private readonly ApplicationDbContext _context;
        private readonly IZwavSagAnalyzer _analyzer;

        public ZwavSagEventService(ApplicationDbContext context, IZwavSagAnalyzer analyzer)
        {
            _context = context;
            _analyzer = analyzer;
        }

        public async Task<PagedResult<ZwavSagListItemDto>> QueryAsync(
            string keyword,
            string eventType,
            string phase,
            DateTime? fromUtc,
            DateTime? toUtc,
            int page,
            int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _context.ZwavSagEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.OriginalName.Contains(keyword) ||
                    x.EventType.Contains(keyword) ||
                    (x.ErrorMessage != null && x.ErrorMessage.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(x => x.EventType == eventType);
            }

            if (!string.IsNullOrWhiteSpace(phase))
            {
                query = query.Where(x =>
                    _context.ZwavSagEventPhases.Any(p => p.SagEventId == x.Id && p.Phase == phase));
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(x => (x.OccurTimeUtc.HasValue ? x.OccurTimeUtc.Value : x.CrtTime) >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => (x.OccurTimeUtc.HasValue ? x.OccurTimeUtc.Value : x.CrtTime) <= toUtc.Value);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CrtTime)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ZwavSagListItemDto
                {
                    Id = x.Id,
                    FileId = x.FileId,
                    OriginalName = x.OriginalName,
                    Status = x.Status,
                    HasSag = x.HasSag,
                    EventType = x.EventType,
                    EventCount = x.EventCount,
                    StartTime = x.StartTime,
                    FinishTime = x.FinishTime,
                    CostMs = x.CostMs,
                    TriggerPhase = x.TriggerPhase,
                    EndPhase = x.EndPhase,
                    WorstPhase = x.WorstPhase,
                    ResidualVoltagePct = x.ResidualVoltagePct,
                    CrtTime = x.CrtTime
                })
                .ToListAsync();

            return new PagedResult<ZwavSagListItemDto>
            {
                Data = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<AnalyzeZwavSagResponse> AnalyzeAsync(AnalyzeZwavSagRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            var fileIds = await ResolveFileIdsAsync(req);
            if (fileIds.Count == 0)
                throw new InvalidOperationException("未选择录波文件");

            int analyzedCount = 0;
            int createdEventCount = 0;
            int createdPhaseCount = 0;
            int createdRmsPointCount = 0;

            foreach (var fileId in fileIds)
            {
                var file = await _context.ZwavFiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == fileId);

                if (file == null)
                    continue;

                var analysis = await _context.ZwavAnalyses
                    .AsNoTracking()
                    .Where(x => x.FileId == fileId)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                if (analysis == null)
                    continue;

                if (req.ForceRebuild)
                {
                    await DeleteByFileIdAsync(fileId);
                }

                var startAt = DateTime.UtcNow;

                try
                {
                    var context = await BuildAnalyzeContextAsync(analysis, req);
                    var analyzeResult = await _analyzer.AnalyzeAsync(context);

                    var finishAt = DateTime.UtcNow;
                    var costMs = (long)Math.Round((finishAt - startAt).TotalMilliseconds);
                    int? rmsEventId = null;

                    if (analyzeResult.Events == null || analyzeResult.Events.Count == 0)
                    {
                        var normal = new ZwavSagEvent
                        {
                            FileId = fileId,
                            OriginalName = file.OriginalName,
                            Status = 2,
                            ErrorMessage = null,
                            HasSag = false,
                            EventType = "Normal",
                            EventCount = 0,
                            StartTime = startAt,
                            FinishTime = finishAt,
                            CostMs = costMs,
                            IsMergedStatEvent = false,
                            MergeGroupId = null,
                            RawEventCount = 0,
                            SeqNo = 0,
                            Remark = null,
                            CrtTime = finishAt,
                            UpdTime = finishAt
                        };

                        _context.ZwavSagEvents.Add(normal);
                        await _context.SaveChangesAsync();

                        createdEventCount++;
                        rmsEventId = normal.Id;
                    }
                    else
                    {
                        int seqNo = 1;
                        foreach (var evt in analyzeResult.Events)
                        {
                            var entity = new ZwavSagEvent
                            {
                                FileId = fileId,
                                OriginalName = file.OriginalName,
                                Status = 2,
                                ErrorMessage = null,
                                HasSag = true,
                                EventType = evt.EventType,
                                EventCount = analyzeResult.Events.Count,
                                StartTime = startAt,
                                FinishTime = finishAt,
                                CostMs = costMs,
                                StartTimeUtc = evt.StartTimeUtc,
                                EndTimeUtc = evt.EndTimeUtc,
                                OccurTimeUtc = evt.OccurTimeUtc,
                                DurationMs = evt.DurationMs,
                                TriggerPhase = evt.TriggerPhase,
                                EndPhase = evt.EndPhase,
                                WorstPhase = evt.WorstPhase,
                                ReferenceType = evt.ReferenceType,
                                ReferenceVoltage = evt.ReferenceVoltage,
                                ResidualVoltage = evt.ResidualVoltage,
                                ResidualVoltagePct = evt.ResidualVoltagePct,
                                SagDepth = evt.SagDepth,
                                SagPercent = evt.SagPercent,
                                StartAngleDeg = evt.StartAngleDeg,
                                PhaseJumpDeg = evt.PhaseJumpDeg,
                                SagThresholdPct = evt.SagThresholdPct,
                                InterruptThresholdPct = evt.InterruptThresholdPct,
                                HysteresisPct = evt.HysteresisPct,
                                IsMergedStatEvent = evt.IsMergedStatEvent,
                                MergeGroupId = evt.MergeGroupId,
                                RawEventCount = evt.RawEventCount,
                                SeqNo = seqNo++,
                                Remark = null,
                                CrtTime = finishAt,
                                UpdTime = finishAt
                            };

                            _context.ZwavSagEvents.Add(entity);
                            await _context.SaveChangesAsync();

                            createdEventCount++;
                            if (!rmsEventId.HasValue) rmsEventId = entity.Id;

                            if (evt.Phases != null && evt.Phases.Count > 0)
                            {
                                int phaseSeq = 1;
                                foreach (var phase in evt.Phases)
                                {
                                    _context.ZwavSagEventPhases.Add(new ZwavSagEventPhase
                                    {
                                        SagEventId = entity.Id,
                                        SeqNo = phaseSeq++,
                                        Phase = phase.Phase,
                                        StartTimeUtc = phase.StartTimeUtc,
                                        EndTimeUtc = phase.EndTimeUtc,
                                        DurationMs = phase.DurationMs,
                                        ReferenceType = phase.ReferenceType,
                                        ReferenceVoltage = phase.ReferenceVoltage,
                                        ResidualVoltage = phase.ResidualVoltage,
                                        ResidualVoltagePct = phase.ResidualVoltagePct,
                                        SagDepth = phase.SagDepth,
                                        SagPercent = phase.SagPercent,
                                        StartAngleDeg = phase.StartAngleDeg,
                                        PhaseJumpDeg = phase.PhaseJumpDeg,
                                        SagThresholdPct = phase.SagThresholdPct,
                                        InterruptThresholdPct = phase.InterruptThresholdPct,
                                        HysteresisPct = phase.HysteresisPct,
                                        IsTriggerPhase = phase.IsTriggerPhase,
                                        IsEndPhase = phase.IsEndPhase,
                                        IsWorstPhase = phase.IsWorstPhase,
                                        CrtTime = finishAt,
                                        UpdTime = finishAt
                                    });
                                    createdPhaseCount++;
                                }
                            }

                            await _context.SaveChangesAsync();
                        }
                    }

                    if (rmsEventId.HasValue && analyzeResult.RmsPoints != null && analyzeResult.RmsPoints.Count > 0)
                    {
                        foreach (var p in analyzeResult.RmsPoints)
                        {
                            _context.ZwavSagRmsPoints.Add(new ZwavSagRmsPoint
                            {
                                SagEventId = rmsEventId.Value,
                                ChannelIndex = p.ChannelIndex,
                                Phase = p.Phase,
                                SampleNo = p.SampleNo,
                                TimeMs = p.TimeMs,
                                Rms = p.Rms,
                                RmsPct = p.RmsPct,
                                ReferenceVoltage = p.ReferenceVoltage,
                                SeqNo = p.SeqNo,
                                CrtTime = finishAt,
                                UpdTime = finishAt
                            });
                            createdRmsPointCount++;
                        }

                        await _context.SaveChangesAsync();
                    }

                    analyzedCount++;
                }
                catch (Exception ex)
                {
                    var failAt = DateTime.UtcNow;

                    _context.ZwavSagEvents.Add(new ZwavSagEvent
                    {
                        FileId = fileId,
                        OriginalName = file.OriginalName,
                        Status = 3,
                        ErrorMessage = ex.Message,
                        HasSag = false,
                        EventType = "Failed",
                        EventCount = 0,
                        StartTime = startAt,
                        FinishTime = failAt,
                        CostMs = (long)Math.Round((failAt - startAt).TotalMilliseconds),
                        IsMergedStatEvent = false,
                        MergeGroupId = null,
                        RawEventCount = 0,
                        SeqNo = 0,
                        Remark = null,
                        CrtTime = failAt,
                        UpdTime = failAt
                    });

                    await _context.SaveChangesAsync();
                }
            }

            return new AnalyzeZwavSagResponse
            {
                AnalyzedCount = analyzedCount,
                CreatedEventCount = createdEventCount,
                CreatedPhaseCount = createdPhaseCount,
                CreatedRmsPointCount = createdRmsPointCount
            };
        }

        private async Task<List<int>> ResolveFileIdsAsync(AnalyzeZwavSagRequest req)
        {
            var fileIds = new HashSet<int>();

            if (req.FileIds != null && req.FileIds.Length > 0)
            {
                foreach (var id in req.FileIds)
                {
                    if (id > 0) fileIds.Add(id);
                }
            }

            if (req.AnalysisGuids != null && req.AnalysisGuids.Length > 0)
            {
                var guids = req.AnalysisGuids
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (guids.Length > 0)
                {
                    var analysisFileIds = await _context.ZwavAnalyses
                        .AsNoTracking()
                        .Where(x => guids.Contains(x.AnalysisGuid))
                        .Select(x => x.FileId)
                        .Distinct()
                        .ToListAsync();

                    foreach (var id in analysisFileIds)
                    {
                        if (id > 0) fileIds.Add(id);
                    }
                }
            }

            return fileIds.ToList();
        }

        private async Task<ZwavSagAnalyzeContext> BuildAnalyzeContextAsync(
            ZwavAnalysis analysis,
            AnalyzeZwavSagRequest req)
        {
            if (analysis == null)
                throw new ArgumentNullException(nameof(analysis));

            int analysisId = analysis.Id;
            var cfg = await _context.ZwavCfgs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == analysisId);

            var voltageChannels = await BuildVoltageChannelsAsync(analysisId);
            if (voltageChannels.Length == 0)
                throw new InvalidOperationException("未识别到电压通道，无法进行暂降分析");

            var rawRows = await LoadWaveRowsAsync(analysisId);
            if (rawRows.Count == 0)
                throw new InvalidOperationException("未读取到波形采样数据");

            var samples = BuildSamples(rawRows, voltageChannels);
            if (samples.Count == 0)
                throw new InvalidOperationException("采样点构建失败");

            decimal frequencyHz = 50m;
            if (cfg?.FrequencyHz.HasValue == true && cfg.FrequencyHz.Value > 0)
                frequencyHz = cfg.FrequencyHz.Value;

            decimal timeMul = 0.001m;
            if (cfg?.TimeMul.HasValue == true && cfg.TimeMul.Value > 0)
                timeMul = cfg.TimeMul.Value;

            var ctx = new ZwavSagAnalyzeContext
            {
                AnalysisId = analysisId,
                FrequencyHz = frequencyHz,
                TimeMul = timeMul,
                WaveStartTimeUtc = analysis.StartTime ?? analysis.CrtTime,
                TriggerTimeUtc = analysis.StartTime,
                ReferenceType = string.IsNullOrWhiteSpace(req.ReferenceType) ? "Sliding" : req.ReferenceType,
                ReferenceVoltage = req.ReferenceVoltage,
                SagThresholdPct = req.SagThresholdPct > 0 ? req.SagThresholdPct : 90m,
                InterruptThresholdPct = req.InterruptThresholdPct > 0 ? req.InterruptThresholdPct : 10m,
                HysteresisPct = req.HysteresisPct >= 0 ? req.HysteresisPct : 2m,
                MinDurationMs = req.MinDurationMs >= 0 ? req.MinDurationMs : 10m,
                RmsMode = "Fixed-OneCycle-HalfCycleStep",
                VoltageChannels = voltageChannels,
                Samples = samples
            };

            LogAnalyzeContext(ctx);
            return ctx;
        }

        private async Task<ZwavVoltageChannelContext[]> BuildVoltageChannelsAsync(int analysisId)
        {
            var channels = await _context.ZwavChannels
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId)
                .Where(x => x.IsEnable == 1)
                .Where(x => x.ChannelType == "Analog")
                .OrderBy(x => x.ChannelIndex)
                .ToListAsync();

            var result = new List<ZwavVoltageChannelContext>();

            foreach (var ch in channels)
            {
                var channelName = (ch.ChannelName ?? string.Empty).Trim();
                var channelCode = (ch.ChannelCode ?? string.Empty).Trim();
                var unit = (ch.Unit ?? string.Empty).Trim();

                if (!IsVoltageChannel(channelName, channelCode, unit))
                    continue;

                var phase = ResolvePhase(channelName, channelCode);
                if (string.IsNullOrWhiteSpace(phase))
                    continue;

                result.Add(new ZwavVoltageChannelContext
                {
                    ChannelIndex = ch.ChannelIndex,
                    Phase = phase,
                    ChannelCode = channelCode,
                    ChannelName = channelName,
                    Unit = unit
                });
            }

            return result.ToArray();
        }

        private static bool IsVoltageChannel(string channelName, string channelCode, string unit)
        {
            var text = $"{channelName} {channelCode}".ToUpperInvariant();

            if (text.Contains("电压")) return true;
            if (text.Contains("保护电压")) return true;
            if (text.Contains("UA")) return true;
            if (text.Contains("UB")) return true;
            if (text.Contains("UC")) return true;

            var u = (unit ?? string.Empty).Trim().ToUpperInvariant();
            if ((u == "V" || u == "KV") &&
                (text.Contains("A相") || text.Contains("B相") || text.Contains("C相")))
                return true;

            return false;
        }

        private static string ResolvePhase(string channelName, string channelCode)
        {
            var text = $"{channelName} {channelCode}".ToUpperInvariant();

            if (text.Contains("A相") || text.Contains("(UA)") || text.EndsWith("UA") || text.Contains(" UA"))
                return "A";
            if (text.Contains("B相") || text.Contains("(UB)") || text.EndsWith("UB") || text.Contains(" UB"))
                return "B";
            if (text.Contains("C相") || text.Contains("(UC)") || text.EndsWith("UC") || text.Contains(" UC"))
                return "C";

            if (text.Contains("AB")) return "AB";
            if (text.Contains("BC")) return "BC";
            if (text.Contains("CA")) return "CA";

            return string.Empty;
        }

        private async Task<List<WaveRowRaw>> LoadWaveRowsAsync(int analysisId)
        {
            // 这里请按你的真实波形表实体替换
            // 假设 _context.ZwavDatas 可映射到 WaveRowRaw
            return await _context.ZwavDatas
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId)
                .OrderBy(x => x.SampleNo)
                .Select(x => new WaveRowRaw
                {
                    SampleNo = x.SampleNo,
                    TimeRaw = x.TimeRaw,
                    TimeMs = x.TimeMs,
                    Channel1 = x.Channel1,
                    Channel2 = x.Channel2,
                    Channel3 = x.Channel3,
                    Channel4 = x.Channel4,
                    Channel5 = x.Channel5,
                    Channel6 = x.Channel6,
                    Channel7 = x.Channel7,
                    Channel8 = x.Channel8,
                    Channel9 = x.Channel9,
                    Channel10 = x.Channel10,
                    Channel11 = x.Channel11,
                    Channel12 = x.Channel12,
                    Channel13 = x.Channel13,
                    Channel14 = x.Channel14,
                    Channel15 = x.Channel15,
                    Channel16 = x.Channel16,
                    Channel17 = x.Channel17,
                    Channel18 = x.Channel18,
                    Channel19 = x.Channel19,
                    Channel20 = x.Channel20,
                    Channel21 = x.Channel21,
                    Channel22 = x.Channel22,
                    Channel23 = x.Channel23,
                    Channel24 = x.Channel24,
                    Channel25 = x.Channel25,
                    Channel26 = x.Channel26,
                    Channel27 = x.Channel27,
                    Channel28 = x.Channel28,
                    Channel29 = x.Channel29,
                    Channel30 = x.Channel30,
                    Channel31 = x.Channel31,
                    Channel32 = x.Channel32,
                    Channel33 = x.Channel33,
                    Channel34 = x.Channel34,
                    Channel35 = x.Channel35,
                    Channel36 = x.Channel36,
                    Channel37 = x.Channel37,
                    Channel38 = x.Channel38,
                    Channel39 = x.Channel39,
                    Channel40 = x.Channel40,
                    Channel41 = x.Channel41,
                    Channel42 = x.Channel42,
                    Channel43 = x.Channel43,
                    Channel44 = x.Channel44,
                    Channel45 = x.Channel45,
                    Channel46 = x.Channel46,
                    Channel47 = x.Channel47,
                    Channel48 = x.Channel48,
                    Channel49 = x.Channel49,
                    Channel50 = x.Channel50,
                    Channel51 = x.Channel51,
                    Channel52 = x.Channel52,
                    Channel53 = x.Channel53,
                    Channel54 = x.Channel54,
                    Channel55 = x.Channel55,
                    Channel56 = x.Channel56,
                    Channel57 = x.Channel57,
                    Channel58 = x.Channel58,
                    Channel59 = x.Channel59,
                    Channel60 = x.Channel60,
                    Channel61 = x.Channel61,
                    Channel62 = x.Channel62,
                    Channel63 = x.Channel63,
                    Channel64 = x.Channel64,
                    Channel65 = x.Channel65,
                    Channel66 = x.Channel66,
                    Channel67 = x.Channel67,
                    Channel68 = x.Channel68,
                    Channel69 = x.Channel69,
                    Channel70 = x.Channel70,
                    Channel71 = x.Channel71,
                    Channel72 = x.Channel72,
                    Channel73 = x.Channel73,
                    Channel74 = x.Channel74,
                    Channel75 = x.Channel75,
                    Channel76 = x.Channel76,
                    Channel77 = x.Channel77,
                    Channel78 = x.Channel78,
                    Channel79 = x.Channel79,
                    Channel80 = x.Channel80,
                    Channel81 = x.Channel81,
                    Channel82 = x.Channel82,
                    Channel83 = x.Channel83,
                    Channel84 = x.Channel84,
                    Channel85 = x.Channel85,
                    Channel86 = x.Channel86,
                    Channel87 = x.Channel87,
                    Channel88 = x.Channel88,
                    Channel89 = x.Channel89,
                    Channel90 = x.Channel90,
                    Channel91 = x.Channel91,
                    Channel92 = x.Channel92,
                    Channel93 = x.Channel93,
                    Channel94 = x.Channel94,
                    Channel95 = x.Channel95,
                    Channel96 = x.Channel96,
                    Channel97 = x.Channel97,
                    Channel98 = x.Channel98,
                    Channel99 = x.Channel99,
                    Channel100 = x.Channel100,
                    DigitalWords = x.DigitalWords
                })
                .ToListAsync();
        }

        private List<ZwavSagSamplePoint> BuildSamples(
            List<WaveRowRaw> rawRows,
            ZwavVoltageChannelContext[] voltageChannels)
        {
            if (rawRows == null || rawRows.Count == 0)
                return new List<ZwavSagSamplePoint>();

            if (voltageChannels == null || voltageChannels.Length == 0)
                return new List<ZwavSagSamplePoint>();

            var channelIndexes = voltageChannels
                .Select(x => x.ChannelIndex)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var result = new List<ZwavSagSamplePoint>(rawRows.Count);

            foreach (var row in rawRows.OrderBy(x => x.SampleNo))
            {
                var values = new Dictionary<int, double?>();

                foreach (var idx in channelIndexes)
                {
                    values[idx] = row.GetAnalogNullable(idx);
                }

                double timeMs = row.TimeMs;
                if (timeMs <= 0 && row.TimeRaw > 0)
                {
                    // 兜底：假设 TimeRaw 单位为微秒
                    timeMs = row.TimeRaw / 1000.0;
                }

                result.Add(new ZwavSagSamplePoint
                {
                    SampleNo = row.SampleNo,
                    TimeRaw = row.TimeRaw,
                    TimeMs = timeMs,
                    ChannelValues = values
                });
            }

            return result;
        }

        private void LogAnalyzeContext(ZwavSagAnalyzeContext ctx)
        {
            Console.WriteLine($"[Sag] AnalysisId={ctx.AnalysisId}");
            Console.WriteLine($"[Sag] FrequencyHz={ctx.FrequencyHz}, TimeMul={ctx.TimeMul}");
            Console.WriteLine($"[Sag] VoltageChannels={string.Join(", ", ctx.VoltageChannels.Select(x => $"{x.ChannelIndex}:{x.ChannelName}:{x.Phase}"))}");
            Console.WriteLine($"[Sag] Samples={ctx.Samples.Count}");

            if (ctx.Samples.Count >= 2)
            {
                Console.WriteLine($"[Sag] FirstSample TimeRaw={ctx.Samples[0].TimeRaw}, TimeMs={ctx.Samples[0].TimeMs}");
                Console.WriteLine($"[Sag] SecondSample TimeRaw={ctx.Samples[1].TimeRaw}, TimeMs={ctx.Samples[1].TimeMs}");
                Console.WriteLine($"[Sag] SampleIntervalMs={ctx.Samples[1].TimeMs - ctx.Samples[0].TimeMs}");
            }
        }

        public async Task<ZwavSagDetailDto> GetDetailAsync(int id)
        {
            var x = await _context.ZwavSagEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (x == null)
                return null;

            return new ZwavSagDetailDto
            {
                Id = x.Id,
                FileId = x.FileId,
                OriginalName = x.OriginalName,
                Status = x.Status,
                ErrorMessage = x.ErrorMessage,
                HasSag = x.HasSag,
                EventType = x.EventType,
                EventCount = x.EventCount,
                StartTime = x.StartTime,
                FinishTime = x.FinishTime,
                CostMs = x.CostMs,
                StartTimeUtc = x.StartTimeUtc,
                EndTimeUtc = x.EndTimeUtc,
                OccurTimeUtc = x.OccurTimeUtc,
                DurationMs = x.DurationMs,
                TriggerPhase = x.TriggerPhase,
                EndPhase = x.EndPhase,
                WorstPhase = x.WorstPhase,
                ReferenceType = x.ReferenceType,
                ReferenceVoltage = x.ReferenceVoltage,
                ResidualVoltage = x.ResidualVoltage,
                ResidualVoltagePct = x.ResidualVoltagePct,
                SagDepth = x.SagDepth,
                SagPercent = x.SagPercent,
                PhaseJumpDeg = x.PhaseJumpDeg,
                StartAngleDeg = x.StartAngleDeg,
                SagThresholdPct = x.SagThresholdPct,
                InterruptThresholdPct = x.InterruptThresholdPct,
                HysteresisPct = x.HysteresisPct,
                IsMergedStatEvent = x.IsMergedStatEvent,
                MergeGroupId = x.MergeGroupId,
                RawEventCount = x.RawEventCount,
                Remark = x.Remark,
                CrtTime = x.CrtTime
            };
        }

        public async Task<ZwavSagPhaseDto[]> GetPhasesAsync(int id)
        {
            return await _context.ZwavSagEventPhases
                .AsNoTracking()
                .Where(x => x.SagEventId == id)
                .OrderBy(x => x.SeqNo)
                .ThenBy(x => x.Phase)
                .Select(x => new ZwavSagPhaseDto
                {
                    Phase = x.Phase,
                    StartTimeUtc = x.StartTimeUtc,
                    EndTimeUtc = x.EndTimeUtc,
                    DurationMs = x.DurationMs,
                    ReferenceVoltage = x.ReferenceVoltage,
                    ResidualVoltage = x.ResidualVoltage,
                    ResidualVoltagePct = x.ResidualVoltagePct,
                    SagDepth = x.SagDepth,
                    SagPercent = x.SagPercent,
                    IsTriggerPhase = x.IsTriggerPhase,
                    IsEndPhase = x.IsEndPhase,
                    IsWorstPhase = x.IsWorstPhase
                })
                .ToArrayAsync();
        }

        public async Task<ZwavSagDetailDto[]> GetByFileIdAsync(int fileId)
        {
            return await _context.ZwavSagEvents
                .AsNoTracking()
                .Where(x => x.FileId == fileId)
                .OrderByDescending(x => x.CrtTime)
                .ThenByDescending(x => x.Id)
                .Select(x => new ZwavSagDetailDto
                {
                    Id = x.Id,
                    FileId = x.FileId,
                    OriginalName = x.OriginalName,
                    Status = x.Status,
                    ErrorMessage = x.ErrorMessage,
                    HasSag = x.HasSag,
                    EventType = x.EventType,
                    EventCount = x.EventCount,
                    StartTime = x.StartTime,
                    FinishTime = x.FinishTime,
                    CostMs = x.CostMs,
                    StartTimeUtc = x.StartTimeUtc,
                    EndTimeUtc = x.EndTimeUtc,
                    OccurTimeUtc = x.OccurTimeUtc,
                    DurationMs = x.DurationMs,
                    TriggerPhase = x.TriggerPhase,
                    EndPhase = x.EndPhase,
                    WorstPhase = x.WorstPhase,
                    ReferenceType = x.ReferenceType,
                    ReferenceVoltage = x.ReferenceVoltage,
                    ResidualVoltage = x.ResidualVoltage,
                    ResidualVoltagePct = x.ResidualVoltagePct,
                    SagDepth = x.SagDepth,
                    SagPercent = x.SagPercent,
                    PhaseJumpDeg = x.PhaseJumpDeg,
                    StartAngleDeg = x.StartAngleDeg,
                    SagThresholdPct = x.SagThresholdPct,
                    InterruptThresholdPct = x.InterruptThresholdPct,
                    HysteresisPct = x.HysteresisPct,
                    IsMergedStatEvent = x.IsMergedStatEvent,
                    MergeGroupId = x.MergeGroupId,
                    RawEventCount = x.RawEventCount,
                    Remark = x.Remark,
                    CrtTime = x.CrtTime
                })
                .ToArrayAsync();
        }

        public async Task<bool> UpdateAsync(int id, UpdateZwavSagEventRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            var entity = await _context.ZwavSagEvents.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
                return false;

            entity.Remark = req.Remark;
            entity.UpdTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.ZwavSagEvents.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
                return false;

            var phases = await _context.ZwavSagEventPhases
                .Where(x => x.SagEventId == id)
                .ToListAsync();

            if (phases.Count > 0)
                _context.ZwavSagEventPhases.RemoveRange(phases);

            var rmsPoints = await _context.ZwavSagRmsPoints
                .Where(x => x.SagEventId == id)
                .ToListAsync();

            if (rmsPoints.Count > 0)
                _context.ZwavSagRmsPoints.RemoveRange(rmsPoints);

            _context.ZwavSagEvents.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> DeleteByFileIdAsync(int fileId)
        {
            var events = await _context.ZwavSagEvents
                .Where(x => x.FileId == fileId)
                .ToListAsync();

            if (events.Count == 0)
                return 0;

            var eventIds = events.Select(x => x.Id).ToList();

            var phases = await _context.ZwavSagEventPhases
                .Where(x => eventIds.Contains(x.SagEventId))
                .ToListAsync();


            if (phases.Count > 0)
                _context.ZwavSagEventPhases.RemoveRange(phases);

            var rmsPoints = await _context.ZwavSagRmsPoints
                .Where(x => eventIds.Contains(x.SagEventId))
                .ToListAsync();

            if (rmsPoints.Count > 0)
                _context.ZwavSagRmsPoints.RemoveRange(rmsPoints);

            _context.ZwavSagEvents.RemoveRange(events);
            await _context.SaveChangesAsync();

            return events.Count;
        }
    }
}
