using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Zwav.Application.Sag;
using Zwav.Application.Parsing;
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

            var query =
                from evt in _context.ZwavSagEvents.AsNoTracking()
                join analysis in _context.ZwavAnalyses.AsNoTracking() on evt.AnalysisId equals analysis.Id
                join file in _context.ZwavFiles.AsNoTracking() on analysis.FileId equals file.Id
                select new
                {
                    Event = evt,
                    FileName = file.OriginalName
                };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.FileName.Contains(keyword) ||
                    x.Event.EventType.Contains(keyword) ||
                    (x.Event.TriggerPhase != null && x.Event.TriggerPhase.Contains(keyword)) ||
                    (x.Event.EndPhase != null && x.Event.EndPhase.Contains(keyword)) ||
                    (x.Event.WorstPhase != null && x.Event.WorstPhase.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(x => x.Event.EventType == eventType);
            }

            if (!string.IsNullOrWhiteSpace(phase))
            {
                query = query.Where(x =>
                    _context.ZwavSagEventPhases.Any(p =>
                        p.SagEventId == x.Event.Id && p.Phase == phase));
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(x => x.Event.OccurTimeUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(x => x.Event.OccurTimeUtc <= toUtc.Value);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.Event.OccurTimeUtc)
                .ThenByDescending(x => x.Event.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ZwavSagListItemDto
                {
                    Id = x.Event.Id,
                    AnalysisId = x.Event.AnalysisId,
                    OriginalName = x.FileName,
                    EventType = x.Event.EventType,
                    OccurTimeUtc = x.Event.OccurTimeUtc,
                    SagPercent = x.Event.SagPercent,
                    DurationMs = x.Event.DurationMs,
                    TriggerPhase = x.Event.TriggerPhase,
                    EndPhase = x.Event.EndPhase,
                    WorstPhase = x.Event.WorstPhase,
                    ResidualVoltagePct = x.Event.ResidualVoltagePct
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

            var result = new AnalyzeZwavSagResponse();

            foreach (var guid in req.AnalysisGuids)
            {
                if (string.IsNullOrWhiteSpace(guid)) continue;

                var analysis = await _context.ZwavAnalyses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.AnalysisGuid == guid);

                if (analysis == null) continue; // Skip if not found

                await AnalyzeSingleAsync(analysis.Id, req, result);
            }

            return result;
        }

        private async Task AnalyzeSingleAsync(int analysisId, AnalyzeZwavSagRequest req, AnalyzeZwavSagResponse totalResult)
        {
            var analysis = await _context.ZwavAnalyses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == analysisId);

            if (analysis == null) return;

            var cfg = await _context.ZwavCfgs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == analysisId);

            if (cfg == null) return;

            var channels = await _context.ZwavChannels
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId)
                .Where(x => x.IsEnable == 1)
                .Where(x => x.ChannelType == "Analog")
                .ToListAsync();

            var channelRules = await _context.ZwavSagChannelRules
                .AsNoTracking()
                .OrderBy(x => x.SeqNo)
                .Select(x => x.RuleName)
                .ToListAsync();

            channelRules = channelRules
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var voltageChannels = channels
                .Where(x => IsVoltageChannel(x, channelRules))
                .Select(x => new ZwavVoltageChannelContext
                {
                    ChannelIndex = x.ChannelIndex,
                    ChannelCode = x.ChannelCode,
                    ChannelName = x.ChannelName,
                    Phase = x.Phase
                })
                .OrderBy(x => x.ChannelIndex)
                .ToArray();

            if (voltageChannels.Length == 0) return;

            var rawSamples = await _context.ZwavDatas
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId)
                .OrderBy(x => x.SampleNo)
                .ToListAsync();

            if (rawSamples.Count == 0) return;

            var samplePoints = new List<ZwavSagSamplePoint>(rawSamples.Count);
            var channelIndexList = voltageChannels.Select(x => x.ChannelIndex).Distinct().ToArray();

            foreach (var row in rawSamples)
            {
                var values = new Dictionary<int, double?>();

                for (int i = 0; i < channelIndexList.Length; i++)
                {
                    int channelIndex = channelIndexList[i];
                    values[channelIndex] = GetAnalogValue(row, channelIndex);
                }

                samplePoints.Add(new ZwavSagSamplePoint
                {
                    SampleNo = row.SampleNo,
                    TimeMs = row.TimeMs,
                    ChannelValues = values
                });
            }

            var waveStartTimeUtc = ResolveWaveStartTimeUtc(analysis, cfg);
            var frequencyHz = cfg.FrequencyHz.HasValue && cfg.FrequencyHz.Value > 0
                ? cfg.FrequencyHz.Value
                : 50m;

            var context = new ZwavSagAnalyzeContext
            {
                AnalysisId = analysisId,
                Samples = samplePoints,
                VoltageChannels = voltageChannels,
                FrequencyHz = frequencyHz,
                WaveStartTimeUtc = waveStartTimeUtc,
                ReferenceType = string.IsNullOrWhiteSpace(req.ReferenceType) ? "Declared" : req.ReferenceType,
                ReferenceVoltage = req.ReferenceVoltage,
                SagThresholdPct = req.SagThresholdPct <= 0 ? 90m : req.SagThresholdPct,
                InterruptThresholdPct = req.InterruptThresholdPct < 0 ? 10m : req.InterruptThresholdPct,
                HysteresisPct = req.HysteresisPct < 0 ? 2m : req.HysteresisPct,
                MinDurationMs = req.MinDurationMs < 0 ? 0m : req.MinDurationMs,
                RmsMode = string.IsNullOrWhiteSpace(req.RmsMode) ? "HalfCycle" : req.RmsMode
            };

            var analyzeResult = await _analyzer.AnalyzeAsync(context);

            using (IDbContextTransaction tx = await _context.Database.BeginTransactionAsync())
            {
                await DeleteByAnalysisIdInternalAsync(analysisId);

                var now = DateTime.UtcNow;
                var eventEntities = new List<ZwavSagEvent>();
                var phaseEntities = new List<ZwavSagEventPhase>();
                var rmsEntities = new List<ZwavSagRmsPoint>();

                for (int i = 0; i < analyzeResult.RmsPoints.Count; i++)
                {
                    var p = analyzeResult.RmsPoints[i];
                    rmsEntities.Add(new ZwavSagRmsPoint
                    {
                        AnalysisId = analysisId,
                        ChannelIndex = p.ChannelIndex,
                        Phase = p.Phase,
                        SampleNo = p.SampleNo,
                        TimeMs = p.TimeMs,
                        Rms = p.Rms,
                        RmsPct = p.RmsPct,
                        ReferenceVoltage = p.ReferenceVoltage,
                        SeqNo = p.SeqNo <= 0 ? i + 1 : p.SeqNo,
                        CrtTime = now,
                        UpdTime = now
                    });
                }

                if (rmsEntities.Count > 0)
                    await _context.ZwavSagRmsPoints.AddRangeAsync(rmsEntities);

                for (int i = 0; i < analyzeResult.Events.Count; i++)
                {
                    var e = analyzeResult.Events[i];
                    var eventEntity = new ZwavSagEvent
                    {
                        AnalysisId = analysisId,
                        EventType = e.EventType,
                        StartTimeUtc = e.StartTimeUtc,
                        EndTimeUtc = e.EndTimeUtc,
                        OccurTimeUtc = e.OccurTimeUtc,
                        DurationMs = e.DurationMs,
                        TriggerPhase = e.TriggerPhase,
                        EndPhase = e.EndPhase,
                        WorstPhase = e.WorstPhase,
                        ReferenceType = e.ReferenceType,
                        ReferenceVoltage = e.ReferenceVoltage,
                        ResidualVoltage = e.ResidualVoltage,
                        ResidualVoltagePct = e.ResidualVoltagePct,
                        SagDepth = e.SagDepth,
                        SagPercent = e.SagPercent,
                        PhaseJumpDeg = e.PhaseJumpDeg,
                        StartAngleDeg = e.StartAngleDeg,
                        SagThresholdPct = e.SagThresholdPct,
                        InterruptThresholdPct = e.InterruptThresholdPct,
                        HysteresisPct = e.HysteresisPct,
                        IsMergedStatEvent = e.IsMergedStatEvent,
                        MergeGroupId = e.MergeGroupId,
                        RawEventCount = e.RawEventCount <= 0 ? 1 : e.RawEventCount,
                        SeqNo = i + 1,
                        Remark = null,
                        CrtTime = now,
                        UpdTime = now
                    };

                    eventEntities.Add(eventEntity);
                }

                if (eventEntities.Count > 0)
                {
                    await _context.ZwavSagEvents.AddRangeAsync(eventEntities);
                    await _context.SaveChangesAsync();
                }

                // 必须在 eventEntities 保存并获得 ID 后再生成 phases
                // 因为 phases 依赖 SagEventId (数据库自增主键)
                // 而在 AddRangeAsync 后如果不 SaveChanges，Id 还是 0 (对于 Identity 列)
                // 所以上面的 SaveChangesAsync 是必须的。
                
                // 但这里有个潜在问题：如果 ZwavSagEventPhase 的 SagEventId 是外键，
                // 且 eventEntities[i].Id 在 SaveChanges 后已被回填，那么下面的逻辑是没问题的。
                // 只要确保 SaveChanges 确实执行了，且数据库生成了 ID。
                
                // 检查一下是否有可能 phases 没有被正确关联。
                // 之前的代码：
                // var savedEvent = eventEntities[i];
                // SagEventId = savedEvent.Id, 
                
                // 如果是 EF Core，通常 AddRange -> SaveChanges 后，对象上的 Id 会被自动更新。
                // 让我们确认一下 phaseEntities 的生成逻辑。

                for (int i = 0; i < analyzeResult.Events.Count; i++)
                {
                    var eventResult = analyzeResult.Events[i];
                    var savedEvent = eventEntities[i];

                    for (int j = 0; j < eventResult.Phases.Count; j++)
                    {
                        var p = eventResult.Phases[j];
                        phaseEntities.Add(new ZwavSagEventPhase
                        {
                            SagEventId = savedEvent.Id,
                            AnalysisId = analysisId,
                            Phase = p.Phase,
                            StartTimeUtc = p.StartTimeUtc,
                            EndTimeUtc = p.EndTimeUtc,
                            DurationMs = p.DurationMs,
                            ReferenceType = p.ReferenceType,
                            ReferenceVoltage = p.ReferenceVoltage,
                            ResidualVoltage = p.ResidualVoltage,
                            ResidualVoltagePct = p.ResidualVoltagePct,
                            SagDepth = p.SagDepth,
                            SagPercent = p.SagPercent,
                            SagThresholdPct = p.SagThresholdPct,
                            InterruptThresholdPct = p.InterruptThresholdPct,
                            HysteresisPct = p.HysteresisPct,
                            StartAngleDeg = p.StartAngleDeg,
                            PhaseJumpDeg = p.PhaseJumpDeg,
                            IsTriggerPhase = p.IsTriggerPhase,
                            IsEndPhase = p.IsEndPhase,
                            IsWorstPhase = p.IsWorstPhase,
                            SeqNo = j + 1,
                            Remark = null,
                            CrtTime = now,
                            UpdTime = now
                        });
                    }
                }

                if (phaseEntities.Count > 0)
                    await _context.ZwavSagEventPhases.AddRangeAsync(phaseEntities);

                await _context.SaveChangesAsync(); // 再次保存 phases
                tx.Commit();

                totalResult.AnalyzedCount++;
                totalResult.CreatedEventCount += eventEntities.Count;
                totalResult.CreatedPhaseCount += phaseEntities.Count;
                totalResult.CreatedRmsPointCount += rmsEntities.Count;
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
                AnalysisId = x.AnalysisId,
                EventType = x.EventType,
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

        public async Task<ZwavSagDetailDto[]> GetByAnalysisIdAsync(int analysisId)
        {
            return await _context.ZwavSagEvents
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId)
                .OrderBy(x => x.OccurTimeUtc)
                .ThenBy(x => x.SeqNo)
                .Select(x => new ZwavSagDetailDto
                {
                    Id = x.Id,
                    AnalysisId = x.AnalysisId,
                    EventType = x.EventType,
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

            _context.ZwavSagEvents.Remove(entity);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> DeleteByAnalysisIdAsync(int analysisId)
        {
            return await DeleteByAnalysisIdInternalAsync(analysisId);
        }

        private async Task<int> DeleteByAnalysisIdInternalAsync(int analysisId)
        {
            var rmsPoints = await _context.ZwavSagRmsPoints
                .Where(x => x.AnalysisId == analysisId)
                .ToListAsync();

            var phaseItems = await _context.ZwavSagEventPhases
                .Where(x => x.AnalysisId == analysisId)
                .ToListAsync();

            var eventItems = await _context.ZwavSagEvents
                .Where(x => x.AnalysisId == analysisId)
                .ToListAsync();

            if (rmsPoints.Count > 0)
                _context.ZwavSagRmsPoints.RemoveRange(rmsPoints);

            if (phaseItems.Count > 0)
                _context.ZwavSagEventPhases.RemoveRange(phaseItems);

            if (eventItems.Count > 0)
                _context.ZwavSagEvents.RemoveRange(eventItems);

            if (rmsPoints.Count > 0 || phaseItems.Count > 0 || eventItems.Count > 0)
                await _context.SaveChangesAsync();

            return eventItems.Count;
        }

        private bool IsVoltageChannel(ZwavChannel channel, List<string> rules)
        {
            if (channel == null)
                return false;

            if (rules == null || rules.Count == 0)
                return false;

            var channelCode = (channel.ChannelCode ?? string.Empty).ToUpperInvariant();
            var channelName = (channel.ChannelName ?? string.Empty).ToUpperInvariant();

            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule))
                    continue;

                var keyword = rule.Trim().ToUpperInvariant();

                if (channelCode.Contains(keyword) || channelName.Contains(keyword))
                    return true;
            }

            return false;
        }

        private static DateTime ResolveWaveStartTimeUtc(ZwavAnalysis analysis, ZwavCfg cfg)
        {
            if (analysis != null && analysis.CrtTime != default)
            {
                return analysis.CrtTime.Kind == DateTimeKind.Utc
                    ? analysis.CrtTime
                    : DateTime.SpecifyKind(analysis.CrtTime, DateTimeKind.Utc);
            }

            return DateTime.UtcNow;
        }

        private static double? GetAnalogValue(ZwavData row, int channelIndex)
        {
            switch (channelIndex)
            {
                case 1: return row.Channel1;
                case 2: return row.Channel2;
                case 3: return row.Channel3;
                case 4: return row.Channel4;
                case 5: return row.Channel5;
                case 6: return row.Channel6;
                case 7: return row.Channel7;
                case 8: return row.Channel8;
                case 9: return row.Channel9;
                case 10: return row.Channel10;
                case 11: return row.Channel11;
                case 12: return row.Channel12;
                case 13: return row.Channel13;
                case 14: return row.Channel14;
                case 15: return row.Channel15;
                case 16: return row.Channel16;
                case 17: return row.Channel17;
                case 18: return row.Channel18;
                case 19: return row.Channel19;
                case 20: return row.Channel20;
                case 21: return row.Channel21;
                case 22: return row.Channel22;
                case 23: return row.Channel23;
                case 24: return row.Channel24;
                case 25: return row.Channel25;
                case 26: return row.Channel26;
                case 27: return row.Channel27;
                case 28: return row.Channel28;
                case 29: return row.Channel29;
                case 30: return row.Channel30;
                case 31: return row.Channel31;
                case 32: return row.Channel32;
                case 33: return row.Channel33;
                case 34: return row.Channel34;
                case 35: return row.Channel35;
                case 36: return row.Channel36;
                case 37: return row.Channel37;
                case 38: return row.Channel38;
                case 39: return row.Channel39;
                case 40: return row.Channel40;
                case 41: return row.Channel41;
                case 42: return row.Channel42;
                case 43: return row.Channel43;
                case 44: return row.Channel44;
                case 45: return row.Channel45;
                case 46: return row.Channel46;
                case 47: return row.Channel47;
                case 48: return row.Channel48;
                case 49: return row.Channel49;
                case 50: return row.Channel50;
                case 51: return row.Channel51;
                case 52: return row.Channel52;
                case 53: return row.Channel53;
                case 54: return row.Channel54;
                case 55: return row.Channel55;
                case 56: return row.Channel56;
                case 57: return row.Channel57;
                case 58: return row.Channel58;
                case 59: return row.Channel59;
                case 60: return row.Channel60;
                case 61: return row.Channel61;
                case 62: return row.Channel62;
                case 63: return row.Channel63;
                case 64: return row.Channel64;
                case 65: return row.Channel65;
                case 66: return row.Channel66;
                case 67: return row.Channel67;
                case 68: return row.Channel68;
                case 69: return row.Channel69;
                case 70: return row.Channel70;
                case 71: return row.Channel71;
                case 72: return row.Channel72;
                case 73: return row.Channel73;
                case 74: return row.Channel74;
                case 75: return row.Channel75;
                case 76: return row.Channel76;
                case 77: return row.Channel77;
                case 78: return row.Channel78;
                case 79: return row.Channel79;
                case 80: return row.Channel80;
                case 81: return row.Channel81;
                case 82: return row.Channel82;
                case 83: return row.Channel83;
                case 84: return row.Channel84;
                case 85: return row.Channel85;
                case 86: return row.Channel86;
                case 87: return row.Channel87;
                case 88: return row.Channel88;
                case 89: return row.Channel89;
                case 90: return row.Channel90;
                case 91: return row.Channel91;
                case 92: return row.Channel92;
                case 93: return row.Channel93;
                case 94: return row.Channel94;
                case 95: return row.Channel95;
                case 96: return row.Channel96;
                case 97: return row.Channel97;
                case 98: return row.Channel98;
                case 99: return row.Channel99;
                case 100: return row.Channel100;
                default: return null;
            }
        }
    }
}