using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Zwav.Application.Parsing;
using Zwav.Application.Sag;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 暂降事件应用服务。
    /// 负责把“录波解析结果”组织成“可分析上下文”，调用分析器完成识别，并将结果转换成查询、展示和持久化所需的数据结构。
    /// </summary>
    public class ZwavSagEventService : IZwavSagEventService
    {
        /// <summary>
        /// 列表页使用的轻量相别投影。
        /// 只保留挑选主相时需要的字段，避免列表查询把完整相别实体全部加载到内存。
        /// </summary>
        private sealed class ListPhaseProjection
        {
            public int SagEventId { get; set; }

            public int Id { get; set; }

            public bool IsWorstPhase { get; set; }

            public decimal DurationMs { get; set; }

            public decimal SagPercent { get; set; }

            public decimal ResidualVoltage { get; set; }

            public decimal ResidualVoltagePct { get; set; }

            public DateTime StartTimeUtc { get; set; }

            public string Phase { get; set; }
        }

        /// <summary>
        /// 持久化到数据库前的电压安全上限，避免异常计算值写入 decimal 列时溢出。
        /// </summary>
        private const decimal MaxVoltageDbValue = 999999999999.999999m;

        /// <summary>
        /// 百分比类字段的数据库安全上限。
        /// </summary>
        private const decimal MaxPercentDbValue = 9999999.999m;

        /// <summary>
        /// 时长类字段的数据库安全上限。
        /// </summary>
        private const decimal MaxDurationDbValue = 999999999.999m;

        /// <summary>
        /// 相角类字段的数据库安全上限。
        /// </summary>
        private const decimal MaxAngleDbValue = 999999.999999m;

        private readonly ApplicationDbContext _context;
        private readonly IZwavSagAnalyzer _analyzer;
        private readonly IZwavSagAnalysisQueue _sagAnalysisQueue;
        private readonly ILogger<ZwavSagEventService> _logger;

        public ZwavSagEventService(
            ApplicationDbContext context,
            IZwavSagAnalyzer analyzer,
            IZwavSagAnalysisQueue sagAnalysisQueue,
            ILogger<ZwavSagEventService> logger)
        {
            _context = context;
            _analyzer = analyzer;
            _sagAnalysisQueue = sagAnalysisQueue;
            _logger = logger;
        }

        /// <summary>
        /// 查询暂降事件列表。
        /// 返回口径为“每个录波文件最新的一条暂降记录”，避免同一文件的处理中、失败、成功历史同时出现在主列表中。
        /// </summary>
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

            // 第一步：先取每个文件最新的一条事件 ID
            var latestEventIds = await _context.ZwavSagEvents
                .AsNoTracking()
                .GroupBy(x => x.FileId)
                .Select(g => g.Max(x => x.Id))
                .ToListAsync()
                .ConfigureAwait(false);

            var eventQuery = _context.ZwavSagEvents
                .AsNoTracking()
                .Where(x => latestEventIds.Contains(x.Id));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                eventQuery = eventQuery.Where(x =>
                    x.OriginalName.Contains(keyword) ||
                    x.EventType.Contains(keyword) ||
                    (x.ErrorMessage != null && x.ErrorMessage.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                eventType = eventType.Trim();
                eventQuery = eventQuery.Where(x => x.EventType == eventType);
            }

            if (!string.IsNullOrWhiteSpace(phase))
            {
                phase = phase.Trim();
                eventQuery = eventQuery.Where(x =>
                    _context.ZwavSagEventPhases.Any(p => p.SagEventId == x.Id && p.Phase == phase));
            }

            if (fromUtc.HasValue)
            {
                var from = fromUtc.Value;
                eventQuery = eventQuery.Where(x => (x.OccurTimeUtc ?? x.CrtTime) >= from);
            }

            if (toUtc.HasValue)
            {
                var to = toUtc.Value;
                eventQuery = eventQuery.Where(x => (x.OccurTimeUtc ?? x.CrtTime) <= to);
            }

            var total = await eventQuery.CountAsync().ConfigureAwait(false);

            // 第二步：分页取事件基础数据
            var pagedEvents = await eventQuery
                .OrderByDescending(x => x.CrtTime)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.FileId,
                    x.OriginalName,
                    x.Status,
                    x.Progress,
                    x.ErrorMessage,
                    x.HasSag,
                    x.EventType,
                    x.WorstPhase,
                    x.DurationMs,
                    x.SagPercent,
                    x.ResidualVoltage,
                    x.ResidualVoltagePct,
                    x.StartTimeUtc,
                    x.OccurTimeUtc,
                    x.CrtTime
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var eventIds = pagedEvents.Select(x => x.Id).ToList();

            // 第三步：单独查询这些事件的 worst phase
            var phaseRows = await _context.ZwavSagEventPhases
                .AsNoTracking()
                .Where(p => eventIds.Contains(p.SagEventId))
                .Select(p => new ListPhaseProjection
                {
                    SagEventId = p.SagEventId,
                    Id = p.Id,
                    IsWorstPhase = p.IsWorstPhase,
                    DurationMs = p.DurationMs,
                    SagPercent = p.SagPercent,
                    ResidualVoltage = p.ResidualVoltage,
                    ResidualVoltagePct = p.ResidualVoltagePct,
                    StartTimeUtc = p.StartTimeUtc,
                    Phase = p.Phase
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var worstPhaseMap = phaseRows
                .GroupBy(x => x.SagEventId)
                .ToDictionary(
                    g => g.Key,
                    g => SelectListPrimaryPhase(g));

            // 第四步：内存组装 DTO
            var items = pagedEvents
                .Select(e =>
                {
                    worstPhaseMap.TryGetValue(e.Id, out var wp);
                    var worstOccurTimeUtc = wp?.StartTimeUtc ?? e.OccurTimeUtc ?? e.StartTimeUtc;
                    var occurTimeUtc = worstOccurTimeUtc;

                    return new ZwavSagListItemDto
                    {
                        Id = e.Id,
                        FileId = e.FileId,
                        OriginalName = e.OriginalName,
                        Status = e.Status,
                        Progress = e.Progress,
                        ErrorMessage = e.ErrorMessage,
                        HasSag = e.HasSag,
                        EventType = e.EventType,
                        WorstPhase = e.WorstPhase,
                        DurationMs = e.DurationMs ?? wp?.DurationMs,
                        SagPercent = e.SagPercent ?? wp?.SagPercent,
                        ResidualVoltage = e.ResidualVoltage ?? wp?.ResidualVoltage,
                        ResidualVoltagePct = e.ResidualVoltagePct ?? wp?.ResidualVoltagePct,
                        OccurTimeUtc = occurTimeUtc,
                        OccurTimeText = BuildOccurTimeText(worstOccurTimeUtc),
                        CrtTime = e.CrtTime
                    };
                })
                .ToList();

            return new PagedResult<ZwavSagListItemDto>
            {
                Data = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        private static string BuildOccurTimeText(DateTime? worstOccurTimeUtc)
        {
            return FormatDateTimeMs(worstOccurTimeUtc);
        }

        private static ListPhaseProjection SelectListPrimaryPhase(IEnumerable<ListPhaseProjection> phases)
        {
            if (phases == null)
                return null;

            return phases
                .Where(x => x != null)
                .OrderByDescending(x => x.IsWorstPhase)
                .ThenByDescending(x => x.SagPercent)
                .ThenByDescending(x => x.DurationMs)
                .ThenBy(x => x.ResidualVoltagePct)
                .ThenBy(x => x.StartTimeUtc)
                .ThenBy(x => x.Phase)
                .ThenBy(x => x.Id)
                .FirstOrDefault();
        }

        private static string FormatDateTimeMs(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                : "-";
        }

        private static bool TryParseFaultStartTime(string raw, out DateTime value)
        {
            return TryParseZwavDateTime(raw, out value);
        }

        private static bool TryParseZwavDateTime(string raw, out DateTime value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim();
            string[] formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                "yyyy/MM/dd HH:mm:ss",
                "yyyy/MM/dd HH:mm:ss.FFFFFFF",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
                "yyyy-MM-dd,HH:mm:ss",
                "yyyy-MM-dd,HH:mm:ss.FFFFFFF",
                "yyyy/MM/dd,HH:mm:ss",
                "yyyy/MM/dd,HH:mm:ss.FFFFFFF",
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss.FFFFFFF",
                "dd/MM/yyyy,HH:mm:ss",
                "dd/MM/yyyy,HH:mm:ss.FFFFFFF",
                "MM/dd/yyyy HH:mm:ss",
                "MM/dd/yyyy HH:mm:ss.FFFFFFF",
                "MM/dd/yyyy,HH:mm:ss",
                "MM/dd/yyyy,HH:mm:ss.FFFFFFF"
            };

            return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        private static DateTime ResolveWaveStartTimeUtc(ZwavAnalysis analysis, ZwavCfg cfg)
        {
            if (cfg != null && TryParseZwavDateTime(cfg.StartTimeRaw, out var cfgStartTime))
                return cfgStartTime;

            return analysis?.StartTime ?? analysis?.CrtTime ?? DateTime.UtcNow;
        }

        private static DateTime? ResolveTriggerTimeUtc(ZwavAnalysis analysis, ZwavCfg cfg)
        {
            if (cfg != null && TryParseZwavDateTime(cfg.TriggerTimeRaw, out var cfgTriggerTime))
                return cfgTriggerTime;

            return analysis?.StartTime;
        }

        /// <summary>
        /// 发起暂降分析请求。
        /// 该方法只负责校验、去重和入队，不在接口线程中直接执行耗时分析。
        /// </summary>
        public async Task<AnalyzeZwavSagResponse> AnalyzeAsync(AnalyzeZwavSagRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            // 统一把 FileIds / AnalysisGuids 映射成文件维度，后续所有幂等控制都围绕 FileId 进行。
            var fileIds = await ResolveFileIdsAsync(req).ConfigureAwait(false);
            if (fileIds.Count == 0)
                throw new InvalidOperationException("未选择录波文件");

            var files = await _context.ZwavFiles
                .AsNoTracking()
                .Where(x => fileIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id)
                .ConfigureAwait(false);

            var latestAnalysisMap = await _context.ZwavAnalyses
                .AsNoTracking()
                .Where(x => fileIds.Contains(x.FileId))
                .GroupBy(x => x.FileId)
                .Select(g => g.OrderByDescending(x => x.Id).FirstOrDefault())
                .ToDictionaryAsync(x => x.FileId, x => x)
                .ConfigureAwait(false);

            var processingFileIds = await _context.ZwavSagEvents
                .AsNoTracking()
                .Where(x => fileIds.Contains(x.FileId) && x.Status == 1)
                .Select(x => x.FileId)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            var queuedEventIds = new List<int>();
            var queueRequest = CloneAnalyzeRequest(req);
            int queuedCount = 0;

            foreach (var fileId in fileIds)
            {
                // 同一文件若已存在处理中事件，则跳过，避免重复分析。
                if (processingFileIds.Contains(fileId))
                    continue;

                if (!files.TryGetValue(fileId, out var file) || file == null)
                {
                    _logger.LogWarning("暂降分析入队跳过：未找到文件，FileId={FileId}", fileId);
                    continue;
                }

                if (!latestAnalysisMap.TryGetValue(fileId, out var analysis) || analysis == null)
                {
                    _logger.LogWarning("暂降分析入队跳过：未找到解析记录，FileId={FileId}", fileId);
                    continue;
                }

                int processingEventId = 0;
                var startAt = DateTime.UtcNow;

                try
                {
                    // 先落一条“处理中”记录，前端可以立即拿到事件 ID 用于轮询进度。
                    var processingEvent = await CreateProcessingEventAsync(fileId, file.OriginalName, startAt).ConfigureAwait(false);
                    processingEventId = processingEvent.Id;
                    queuedEventIds.Add(processingEventId);

                    // 真正的分析工作异步交由后台队列处理，接口本身只做快速响应。
                    await _sagAnalysisQueue.EnqueueAsync(new ZwavSagAnalysisQueueItem
                    {
                        FileId = fileId,
                        AnalysisId = analysis.Id,
                        ProcessingEventId = processingEventId,
                        OriginalName = file.OriginalName,
                        StartTimeUtc = startAt,
                        Request = CloneAnalyzeRequest(queueRequest)
                    }).ConfigureAwait(false);

                    queuedCount++;
                }
                catch (Exception ex)
                {
                    // 如果“处理中”记录已创建但入队失败，需要及时回写失败状态，防止界面长期停留在处理中。
                    if (processingEventId > 0)
                        await MarkProcessingEventFailedAsync(processingEventId, ex.Message).ConfigureAwait(false);

                    _logger.LogError(
                        ex,
                        "暂降分析入队失败，FileId={FileId}, AnalysisId={AnalysisId}, OriginalName={OriginalName}",
                        fileId,
                        analysis?.Id,
                        file?.OriginalName);

                    throw;
                }
            }

            if (queuedCount == 0)
                throw new InvalidOperationException("所选录波正在分析中，请稍后刷新进度后再试");

            return new AnalyzeZwavSagResponse
            {
                AnalyzedCount = queuedCount,
                QueuedCount = queuedCount,
                QueuedEventIds = queuedEventIds.ToArray(),
                CreatedEventCount = queuedCount,
                CreatedPhaseCount = 0,
                CreatedRmsPointCount = 0
            };
        }

        /// <summary>
        /// 后台处理单个暂降分析任务。
        /// 整体流程是：读取解析结果、构建分析上下文、调用分析器、清理旧结果、写入新结果。
        /// </summary>
        public async Task ProcessQueuedAnalyzeAsync(ZwavSagAnalysisQueueItem item, CancellationToken cancellationToken = default)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var startAt = item.StartTimeUtc == default ? DateTime.UtcNow : item.StartTimeUtc;
            var originalName = item.OriginalName;

            try
            {
                // 重新从数据库拉取文件和解析记录，避免后台任务依赖已经过期的浅拷贝对象。
                var file = await _context.ZwavFiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == item.FileId, cancellationToken)
                    .ConfigureAwait(false);
                if (file == null)
                    throw new InvalidOperationException($"未找到录波文件，FileId={item.FileId}");

                originalName = file.OriginalName;

                var analysis = await _context.ZwavAnalyses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == item.AnalysisId, cancellationToken)
                    .ConfigureAwait(false);
                if (analysis == null)
                    throw new InvalidOperationException($"未找到解析记录，AnalysisId={item.AnalysisId}");

                _logger.LogInformation(
                    "开始后台暂降分析，FileId={FileId}, AnalysisId={AnalysisId}, ProcessingEventId={ProcessingEventId}, OriginalName={OriginalName}",
                    item.FileId,
                    item.AnalysisId,
                    item.ProcessingEventId,
                    originalName);

                // 这里的进度值用于前端显示阶段，不追求严格线性。
                await UpdateProgressAsync(item.ProcessingEventId, 5).ConfigureAwait(false);
                var analyzeContext = await BuildAnalyzeContextAsync(analysis, item.Request ?? new AnalyzeZwavSagRequest()).ConfigureAwait(false);
                await UpdateProgressAsync(item.ProcessingEventId, 25).ConfigureAwait(false);
                var analyzeResult = await _analyzer.AnalyzeAsync(analyzeContext).ConfigureAwait(false);
                await UpdateProgressAsync(item.ProcessingEventId, 70).ConfigureAwait(false);

                var finishAt = DateTime.UtcNow;
                var costMs = (long)Math.Round((finishAt - startAt).TotalMilliseconds);

                using var tx = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                await UpdateProgressAsync(item.ProcessingEventId, 85).ConfigureAwait(false);
                // 同一文件只保留本次分析的最新结果，所以在落库前先清理旧快照。
                await DeleteByFileIdInternalAsync(item.FileId).ConfigureAwait(false);

                var persistPackages = BuildPersistPackages(
                    item.FileId,
                    originalName,
                    startAt,
                    finishAt,
                    costMs,
                    analyzeContext,
                    analyzeResult);

                int fileCreatedEventCount = 0;
                int fileCreatedPhaseCount = 0;
                int fileCreatedRmsCount = 0;
                int firstEventId = 0;

                for (int i = 0; i < persistPackages.Count; i++)
                {
                    var pkg = persistPackages[i];
                    if (pkg?.Event == null)
                        continue;

                    // 先保存主事件拿到主键，再回填相别和 RMS 明细外键。
                    _context.ZwavSagEvents.Add(pkg.Event);
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    if (firstEventId <= 0)
                        firstEventId = pkg.Event.Id;

                    if (pkg.Phases.Count > 0)
                    {
                        for (int j = 0; j < pkg.Phases.Count; j++)
                            pkg.Phases[j].SagEventId = pkg.Event.Id;

                        _context.ZwavSagEventPhases.AddRange(pkg.Phases);
                    }

                    if (pkg.RmsPoints.Count > 0)
                    {
                        for (int j = 0; j < pkg.RmsPoints.Count; j++)
                            pkg.RmsPoints[j].SagEventId = pkg.Event.Id;

                        _context.ZwavSagRmsPoints.AddRange(pkg.RmsPoints);
                    }

                    if (pkg.Phases.Count > 0 || pkg.RmsPoints.Count > 0)
                        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    fileCreatedEventCount++;
                    fileCreatedPhaseCount += pkg.Phases.Count;
                    fileCreatedRmsCount += pkg.RmsPoints.Count;
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "后台暂降分析完成，FileId={FileId}, EventId={SagEventId}, EventCount={EventCount}, PhaseCount={PhaseCount}, RmsCount={RmsCount}, CostMs={CostMs}",
                    item.FileId,
                    firstEventId,
                    fileCreatedEventCount,
                    fileCreatedPhaseCount,
                    fileCreatedRmsCount,
                    costMs);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failAt = DateTime.UtcNow;

                _logger.LogError(
                    ex,
                    "后台暂降分析失败，FileId={FileId}, AnalysisId={AnalysisId}, ProcessingEventId={ProcessingEventId}, OriginalName={OriginalName}",
                    item.FileId,
                    item.AnalysisId,
                    item.ProcessingEventId,
                    originalName);

                _context.ChangeTracker.Clear();
                // 失败时也要清理掉可能已部分写入的新结果，避免数据库中出现半成品分析数据。
                await DeleteByFileIdInternalAsync(item.FileId).ConfigureAwait(false);

                _context.ZwavSagEvents.Add(CreateFailedEvent(item.FileId, originalName, startAt, failAt, ex.Message));
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 获取过程展示页所需的完整分析上下文。
        /// 包括录波配置、识别到的电压通道、原始采样点、RMS 点以及事件结果。
        /// </summary>
        public async Task<ZwavSagProcessDto> GetProcessAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentException("id 必须大于 0");

            var detail = await GetDetailAsync(id).ConfigureAwait(false);
            if (detail == null)
                throw new KeyNotFoundException("事件不存在");

            var analysis = await _context.ZwavAnalyses
                .AsNoTracking()
                .Where(x => x.FileId == detail.FileId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (analysis == null)
                throw new KeyNotFoundException("未找到对应解析任务");

            var cfg = await _context.ZwavCfgs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == analysis.Id)
                .ConfigureAwait(false);

            var hdr = await _context.ZwavHdrs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == analysis.Id)
                .ConfigureAwait(false);

            decimal frequencyHz = (cfg?.FrequencyHz).GetValueOrDefault() > 0 ? cfg.FrequencyHz.Value : 50m;
            decimal timeMul = (cfg?.TimeMul).GetValueOrDefault() > 0 ? cfg.TimeMul.Value : 0.001m;
            var waveStartTimeUtc = ResolveWaveStartTimeUtc(analysis, cfg);

            var voltageChannelsCtx = await BuildVoltageChannelsAsync(analysis.Id).ConfigureAwait(false);
            var voltageChannels = voltageChannelsCtx
                .Select(x => new ZwavSagVoltageChannelDto
                {
                    ChannelIndex = x.ChannelIndex,
                    Phase = x.Phase,
                    ChannelCode = x.ChannelCode,
                    ChannelName = x.ChannelName,
                    Unit = x.Unit
                })
                .ToArray();

            var rmsPoints = await _context.ZwavSagRmsPoints
                .AsNoTracking()
                .Where(x => x.SagEventId == id)
                .OrderBy(x => x.ChannelIndex)
                .ThenBy(x => x.SeqNo)
                .Select(x => new ZwavSagRmsPointDto
                {
                    ChannelIndex = x.ChannelIndex,
                    Phase = x.Phase,
                    SampleNo = x.SampleNo,
                    TimeMs = x.TimeMs,
                    Rms = x.Rms,
                    RmsPct = x.RmsPct,
                    ReferenceVoltage = x.ReferenceVoltage,
                    SeqNo = x.SeqNo
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var phases = await GetPhasesAsync(id).ConfigureAwait(false);
            var processPhases = (phases ?? Array.Empty<ZwavSagPhaseDto>())
                .Where(x => x != null)
                .Select(x => new ZwavSagProcessPhaseDto
                {
                    ChannelIndex = x.ChannelIndex,
                    GroupName = x.GroupName,
                    ChannelName = x.ChannelName,
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
                .ToArray();

            var processTimeAxisStartUtc = NormalizeStoredProcessTimes(
                waveStartTimeUtc,
                analysis.StartTime ?? analysis.CrtTime,
                hdr?.FaultStartTime,
                detail,
                processPhases,
                rmsPoints);

            var computed = BuildComputedEventsFromPhases(
                detail,
                processPhases,
                processTimeAxisStartUtc,
                detail.InterruptThresholdPct ?? 10m);

            var markers = BuildMarkers(detail, processPhases, processTimeAxisStartUtc, rmsPoints, computed);

            // 默认返回整段波形范围，不再按事件局部裁切
            int? suggestedFromSample = rmsPoints.Count > 0
                ? rmsPoints.Min(x => x.SampleNo)
                : (int?)null;

            int? suggestedToSample = rmsPoints.Count > 0
                ? rmsPoints.Max(x => x.SampleNo)
                : (int?)null;

            return new ZwavSagProcessDto
            {
                Event = new ZwavSagProcessEventDto
                {
                    Id = detail.Id,
                    FileId = detail.FileId,
                    OriginalName = detail.OriginalName,
                    Status = detail.Status,
                    ErrorMessage = detail.ErrorMessage,
                    HasSag = detail.HasSag,
                    EventType = detail.EventType,
                    EventCount = detail.EventCount,
                    CostMs = detail.CostMs,
                    WorstPhase = detail.WorstPhase,
                    TriggerPhase = detail.TriggerPhase,
                    EndPhase = detail.EndPhase,
                    StartTimeUtc = detail.StartTimeUtc,
                    EndTimeUtc = detail.EndTimeUtc,
                    OccurTimeUtc = detail.OccurTimeUtc,
                    DurationMs = detail.DurationMs,
                    ReferenceVoltage = detail.ReferenceVoltage,
                    ResidualVoltage = detail.ResidualVoltage,
                    ResidualVoltagePct = detail.ResidualVoltagePct,
                    SagPercent = detail.SagPercent,
                    SagThresholdPct = detail.SagThresholdPct,
                    InterruptThresholdPct = detail.InterruptThresholdPct,
                    HysteresisPct = detail.HysteresisPct,
                    MinDurationMs = 10m
                },
                Phases = processPhases,
                AnalysisId = analysis.Id,
                AnalysisGuid = analysis.AnalysisGuid,
                FaultStartTime = hdr?.FaultStartTime,
                WaveStartTimeUtc = processTimeAxisStartUtc,
                FrequencyHz = frequencyHz,
                TimeMul = timeMul,
                VoltageChannels = voltageChannels,
                RmsPoints = rmsPoints.ToArray(),
                Markers = markers,
                ComputedEvents = computed,
                SuggestedFromSample = suggestedFromSample,
                SuggestedToSample = suggestedToSample
            };
        }

        public async Task<ZwavSagProcessPreviewResponse> PreviewProcessAsync(int id, ZwavSagProcessPreviewRequest req)
        {
            if (id <= 0)
                throw new ArgumentException("id 必须大于 0");

            var detail = await GetDetailAsync(id).ConfigureAwait(false);
            if (detail == null)
                throw new KeyNotFoundException("事件不存在");

            var analysis = await _context.ZwavAnalyses
                .AsNoTracking()
                .Where(x => x.FileId == detail.FileId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (analysis == null)
                throw new KeyNotFoundException("未找到对应解析任务");
            decimal recoverThreshold = req?.RecoverThresholdPct ?? detail.RecoverThresholdPct ?? 92m;
            decimal sagThreshold = req?.SagThresholdPct ?? detail.SagThresholdPct ?? 90m;
            decimal interruptThreshold = req?.InterruptThresholdPct ?? detail.InterruptThresholdPct ?? 10m;
            decimal hysteresis = req?.HysteresisPct ?? detail.HysteresisPct ?? 2m;
            decimal minDuration = req?.MinDurationMs ?? 10m;

            decimal? overrideRef = req?.ReferenceVoltage;
            if (overrideRef.HasValue && overrideRef.Value <= 0)
                overrideRef = null;

            var analyzeReq = new AnalyzeZwavSagRequest
            {
                ForceRebuild = false,
                ReferenceType = NormalizeReferenceType(req?.ReferenceType, overrideRef ?? detail.ReferenceVoltage),
                ReferenceVoltage = overrideRef,
                SagThresholdPct = sagThreshold,
                RecoverThresholdPct = recoverThreshold,
                InterruptThresholdPct = interruptThreshold,
                HysteresisPct = hysteresis,
                MinDurationMs = minDuration
            };

            var analyzeContext = await BuildAnalyzeContextAsync(analysis, analyzeReq).ConfigureAwait(false);
            var analyzeResult = await _analyzer.AnalyzeAsync(analyzeContext).ConfigureAwait(false);

            var waveStartTimeUtc = analyzeContext.WaveStartTimeUtc;

            var rmsPoints = analyzeResult.RmsPoints
                .OrderBy(x => x.ChannelIndex)
                .ThenBy(x => x.SeqNo)
                .Select(x => new ZwavSagRmsPointDto
                {
                    ChannelIndex = x.ChannelIndex,
                    Phase = x.Phase,
                    SampleNo = x.SampleNo,
                    TimeMs = x.TimeMs,
                    Rms = x.Rms,
                    RmsPct = x.RmsPct,
                    ReferenceVoltage = x.ReferenceVoltage,
                    SeqNo = x.SeqNo
                })
                .ToArray();

            var computed = BuildComputedEventsFromAnalyzeResult(analyzeResult, waveStartTimeUtc, interruptThreshold);

            var previewPhases = analyzeResult.Events
                .SelectMany(x => x?.Phases ?? new List<ZwavSagEventPhaseResult>())
                .Where(x => x != null)
                .Select(x => new ZwavSagProcessPhaseDto
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
                .ToArray();

            var markers = BuildMarkers(detail, previewPhases, waveStartTimeUtc, rmsPoints.ToList(), computed);

            // 预览同样返回整段波形范围
            int? suggestedFromSample = rmsPoints.Length > 0
                ? rmsPoints.Min(x => x.SampleNo)
                : (int?)null;

            int? suggestedToSample = rmsPoints.Length > 0
                ? rmsPoints.Max(x => x.SampleNo)
                : (int?)null;

            return new ZwavSagProcessPreviewResponse
            {
                RmsPoints = rmsPoints,
                Markers = markers,
                ComputedEvents = computed,
                SuggestedFromSample = suggestedFromSample,
                SuggestedToSample = suggestedToSample
            };
        }

        public async Task<PagedResult<ZwavSagChannelRuleDto>> QueryChannelRuleAsync(string keyword, int page, int pageSize)
        {
            if (page <= 0)
                page = 1;

            if (pageSize <= 0)
                pageSize = 20;

            if (pageSize > 200)
                pageSize = 200;

            var query = _context.ZwavSagChannelRules.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(x => x.RuleName.Contains(keyword));
            }

            var total = await query.CountAsync().ConfigureAwait(false);

            var items = await query
                .OrderBy(x => x.SeqNo)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ZwavSagChannelRuleDto
                {
                    Id = x.Id,
                    RuleName = x.RuleName,
                    PhaseName = x.PhaseName,
                    SeqNo = x.SeqNo,
                    CrtTime = x.CrtTime
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<ZwavSagChannelRuleDto>
            {
                Data = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<ZwavSagChannelRuleDto> CreateChannelRuleAsync(CreateZwavSagChannelRuleRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            if (string.IsNullOrWhiteSpace(req.RuleName))
                throw new ArgumentException("RuleName 不能为空");

            if (string.IsNullOrWhiteSpace(req.PhaseName))
                throw new ArgumentException("PhaseName 不能为空");

            var ruleName = req.RuleName.Trim();
            var phaseName = NormalizePhaseName(req.PhaseName);
            if (string.IsNullOrWhiteSpace(phaseName))
                throw new ArgumentException("PhaseName 必须为 A/B/C/AB/BC/CA");

            var exists = await _context.ZwavSagChannelRules
                .AnyAsync(x => x.RuleName == ruleName)
                .ConfigureAwait(false);

            if (exists)
                throw new InvalidOperationException("词库规则已存在");

            var now = DateTime.UtcNow;

            var entity = new ZwavSagChannelRule
            {
                RuleName = ruleName,
                PhaseName = phaseName,
                SeqNo = req.SeqNo,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavSagChannelRules.Add(entity);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return new ZwavSagChannelRuleDto
            {
                Id = entity.Id,
                RuleName = entity.RuleName,
                PhaseName = entity.PhaseName,
                SeqNo = entity.SeqNo,
                CrtTime = entity.CrtTime
            };
        }

        public async Task<bool> UpdateChannelRuleAsync(int id, UpdateZwavSagChannelRuleRequest req)
        {
            if (id <= 0)
                throw new ArgumentException("id 必须大于 0");

            if (req == null)
                throw new ArgumentException("请求体不能为空");

            var entity = await _context.ZwavSagChannelRules
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);

            if (entity == null)
                return false;

            if (!string.IsNullOrWhiteSpace(req.RuleName))
            {
                var ruleName = req.RuleName.Trim();

                var exists = await _context.ZwavSagChannelRules
                    .AnyAsync(x => x.Id != id && x.RuleName == ruleName)
                    .ConfigureAwait(false);

                if (exists)
                    throw new InvalidOperationException("词库规则已存在");

                entity.RuleName = ruleName;
            }

            if (!string.IsNullOrWhiteSpace(req.PhaseName))
            {
                var phaseName = NormalizePhaseName(req.PhaseName);
                if (string.IsNullOrWhiteSpace(phaseName))
                    throw new ArgumentException("PhaseName 必须为 A/B/C/AB/BC/CA");
                entity.PhaseName = phaseName;
            }

            if (req.SeqNo.HasValue)
                entity.SeqNo = req.SeqNo.Value;

            entity.UpdTime = DateTime.UtcNow;

            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> DeleteChannelRuleAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentException("id 必须大于 0");

            var entity = await _context.ZwavSagChannelRules
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);

            if (entity == null)
                return false;

            _context.ZwavSagChannelRules.Remove(entity);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<PagedResult<ZwavSagGroupRuleDto>> QueryGroupRuleAsync(string keyword, int page, int pageSize)
        {
            if (page <= 0)
                page = 1;

            if (pageSize <= 0)
                pageSize = 20;

            if (pageSize > 200)
                pageSize = 200;

            var query = _context.ZwavSagGroupRules.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(x => x.RuleName.Contains(keyword) || x.GroupName.Contains(keyword));
            }

            var total = await query.CountAsync().ConfigureAwait(false);

            var items = await query
                .OrderBy(x => x.SeqNo)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ZwavSagGroupRuleDto
                {
                    Id = x.Id,
                    RuleName = x.RuleName,
                    GroupName = x.GroupName,
                    SeqNo = x.SeqNo,
                    CrtTime = x.CrtTime
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<ZwavSagGroupRuleDto>
            {
                Data = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<ZwavSagGroupRuleDto> CreateGroupRuleAsync(CreateZwavSagGroupRuleRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            if (string.IsNullOrWhiteSpace(req.RuleName))
                throw new ArgumentException("RuleName 不能为空");

            if (string.IsNullOrWhiteSpace(req.GroupName))
                throw new ArgumentException("GroupName 不能为空");

            var ruleName = req.RuleName.Trim();
            var groupName = req.GroupName.Trim();

            var exists = await _context.ZwavSagGroupRules
                .AnyAsync(x => x.RuleName == ruleName)
                .ConfigureAwait(false);

            if (exists)
                throw new InvalidOperationException("分组规则已存在");

            var now = DateTime.UtcNow;
            var entity = new ZwavSagGroupRule
            {
                RuleName = ruleName,
                GroupName = groupName,
                SeqNo = req.SeqNo,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavSagGroupRules.Add(entity);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return new ZwavSagGroupRuleDto
            {
                Id = entity.Id,
                RuleName = entity.RuleName,
                GroupName = entity.GroupName,
                SeqNo = entity.SeqNo,
                CrtTime = entity.CrtTime
            };
        }

        public async Task<bool> UpdateGroupRuleAsync(int id, UpdateZwavSagGroupRuleRequest req)
        {
            if (id <= 0)
                throw new ArgumentException("id 必须大于 0");

            if (req == null)
                throw new ArgumentException("请求体不能为空");

            var entity = await _context.ZwavSagGroupRules
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);

            if (entity == null)
                return false;

            if (!string.IsNullOrWhiteSpace(req.RuleName))
            {
                var ruleName = req.RuleName.Trim();
                var exists = await _context.ZwavSagGroupRules
                    .AnyAsync(x => x.Id != id && x.RuleName == ruleName)
                    .ConfigureAwait(false);

                if (exists)
                    throw new InvalidOperationException("分组规则已存在");

                entity.RuleName = ruleName;
            }

            if (!string.IsNullOrWhiteSpace(req.GroupName))
                entity.GroupName = req.GroupName.Trim();

            if (req.SeqNo.HasValue)
                entity.SeqNo = req.SeqNo.Value;

            entity.UpdTime = DateTime.UtcNow;

            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> DeleteGroupRuleAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentException("id 必须大于 0");

            var entity = await _context.ZwavSagGroupRules
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);

            if (entity == null)
                return false;

            _context.ZwavSagGroupRules.Remove(entity);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        private static string NormalizePhaseName(string phaseName)
        {
            var p = (phaseName ?? string.Empty).Trim().ToUpperInvariant();
            if (p == "A" || p == "B" || p == "C" || p == "AB" || p == "BC" || p == "CA")
                return p;
            return string.Empty;
        }

        private static string NormalizeReferenceType(string referenceType, decimal? referenceVoltage)
        {
            var raw = (referenceType ?? string.Empty).Trim();
            if (raw.Length == 0)
                return referenceVoltage.HasValue && referenceVoltage.Value > 0m ? "Declared" : "Sliding";

            if (string.Equals(raw, "Declared", StringComparison.OrdinalIgnoreCase))
                return "Declared";

            if (string.Equals(raw, "Sliding", StringComparison.OrdinalIgnoreCase))
                return "Sliding";

            return referenceVoltage.HasValue && referenceVoltage.Value > 0m ? "Declared" : "Sliding";
        }

        private static DateTime NormalizeStoredProcessTimes(
            DateTime expectedWaveStartTimeUtc,
            DateTime? analysisTaskStartUtc,
            string faultStartTime,
            ZwavSagDetailDto detail,
            ZwavSagProcessPhaseDto[] phases,
            IReadOnlyList<ZwavSagRmsPointDto> rmsPoints)
        {
            var storedBaseUtc = ResolveStoredEventTimeBaseUtc(
                expectedWaveStartTimeUtc,
                analysisTaskStartUtc,
                faultStartTime,
                detail,
                phases,
                rmsPoints);

            if (storedBaseUtc == expectedWaveStartTimeUtc)
                return expectedWaveStartTimeUtc;

            var delta = expectedWaveStartTimeUtc - storedBaseUtc;

            if (detail != null)
            {
                if (detail.StartTimeUtc.HasValue)
                    detail.StartTimeUtc = detail.StartTimeUtc.Value.Add(delta);

                if (detail.EndTimeUtc.HasValue)
                    detail.EndTimeUtc = detail.EndTimeUtc.Value.Add(delta);

                if (detail.OccurTimeUtc.HasValue)
                    detail.OccurTimeUtc = detail.OccurTimeUtc.Value.Add(delta);
            }

            if (phases != null)
            {
                for (int i = 0; i < phases.Length; i++)
                {
                    var phase = phases[i];
                    if (phase == null)
                        continue;

                    phase.StartTimeUtc = phase.StartTimeUtc.Add(delta);
                    phase.EndTimeUtc = phase.EndTimeUtc.Add(delta);
                }
            }

            return expectedWaveStartTimeUtc;
        }

        private static DateTime ResolveStoredEventTimeBaseUtc(
            DateTime expectedWaveStartTimeUtc,
            DateTime? analysisTaskStartUtc,
            string faultStartTime,
            ZwavSagDetailDto detail,
            IEnumerable<ZwavSagProcessPhaseDto> phases,
            IReadOnlyList<ZwavSagRmsPointDto> rmsPoints)
        {
            var eventTimes = ExtractStoredEventTimes(detail, phases);
            if (eventTimes.Count == 0 || rmsPoints == null || rmsPoints.Count == 0)
                return expectedWaveStartTimeUtc;

            double minTimeMs = rmsPoints.Min(x => x.TimeMs);
            double maxTimeMs = rmsPoints.Max(x => x.TimeMs);
            if (maxTimeMs < minTimeMs)
            {
                double tmp = minTimeMs;
                minTimeMs = maxTimeMs;
                maxTimeMs = tmp;
            }

            double toleranceMs = Math.Max(20d, Math.Min(1000d, (maxTimeMs - minTimeMs) * 0.05d));

            var candidates = new List<DateTime> { expectedWaveStartTimeUtc };
            if (analysisTaskStartUtc.HasValue)
                candidates.Add(analysisTaskStartUtc.Value);

            if (TryParseFaultStartTime(faultStartTime, out var faultStart))
                candidates.Add(faultStart);

            var distinctCandidates = candidates
                .Distinct()
                .ToList();

            DateTime bestCandidate = expectedWaveStartTimeUtc;
            double bestScore = double.MaxValue;

            for (int i = 0; i < distinctCandidates.Count; i++)
            {
                var candidate = distinctCandidates[i];
                double score = 0d;

                for (int j = 0; j < eventTimes.Count; j++)
                {
                    double timeMs = (eventTimes[j] - candidate).TotalMilliseconds;
                    score += ComputeTimeAxisPenalty(timeMs, minTimeMs, maxTimeMs, toleranceMs);
                }

                score += Math.Abs((candidate - expectedWaveStartTimeUtc).TotalMilliseconds) * 0.001d;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        private static List<DateTime> ExtractStoredEventTimes(
            ZwavSagDetailDto detail,
            IEnumerable<ZwavSagProcessPhaseDto> phases)
        {
            var result = new List<DateTime>();

            if (phases != null)
            {
                foreach (var phase in phases)
                {
                    if (phase == null)
                        continue;

                    result.Add(phase.StartTimeUtc);
                    result.Add(phase.EndTimeUtc);
                }
            }

            if (detail?.StartTimeUtc.HasValue == true)
                result.Add(detail.StartTimeUtc.Value);

            if (detail?.EndTimeUtc.HasValue == true)
                result.Add(detail.EndTimeUtc.Value);

            if (detail?.OccurTimeUtc.HasValue == true)
                result.Add(detail.OccurTimeUtc.Value);

            return result;
        }

        private static double ComputeTimeAxisPenalty(
            double timeMs,
            double minTimeMs,
            double maxTimeMs,
            double toleranceMs)
        {
            if (timeMs < minTimeMs - toleranceMs)
                return (minTimeMs - toleranceMs) - timeMs;

            if (timeMs > maxTimeMs + toleranceMs)
                return timeMs - (maxTimeMs + toleranceMs);

            return 0d;
        }

        private static ZwavSagComputedEventDto[] BuildComputedEventsFromAnalyzeResult(
            ZwavSagAnalyzeResult analyzeResult,
            DateTime waveStartTimeUtc,
            decimal interruptThresholdPct)
        {
            if (analyzeResult == null || analyzeResult.Events == null || analyzeResult.Events.Count == 0)
                return Array.Empty<ZwavSagComputedEventDto>();

            var list = new List<ZwavSagComputedEventDto>();

            for (int i = 0; i < analyzeResult.Events.Count; i++)
            {
                var evt = analyzeResult.Events[i];
                if (evt == null || evt.Phases == null || evt.Phases.Count == 0)
                    continue;

                for (int j = 0; j < evt.Phases.Count; j++)
                {
                    var p = evt.Phases[j];
                    if (p == null)
                        continue;

                    double startMs = (p.StartTimeUtc - waveStartTimeUtc).TotalMilliseconds;
                    double endMs = (p.EndTimeUtc - waveStartTimeUtc).TotalMilliseconds;
                    decimal worstPct = p.ResidualVoltagePct;
                    decimal interrupt = p.InterruptThresholdPct ?? interruptThresholdPct;
                    string eventType = worstPct <= interrupt ? "Interruption" : "Sag";
                    decimal sagMagnitude = decimal.Round(100m - worstPct, 3);

                    list.Add(new ZwavSagComputedEventDto
                    {
                        EventType = eventType,
                        ChannelIndex = p.ChannelIndex,
                        GroupName = p.GroupName,
                        ChannelName = p.ChannelName,
                        Phase = p.Phase,
                        OccurTimeUtc = evt.OccurTimeUtc,
                        StartTimeUtc = p.StartTimeUtc,
                        EndTimeUtc = p.EndTimeUtc,
                        StartMs = startMs,
                        EndMs = endMs,
                        DurationMs = p.DurationMs,
                        ResidualVoltage = p.ResidualVoltage,
                        ResidualVoltagePct = decimal.Round(worstPct, 3),
                        SagMagnitudePct = sagMagnitude
                    });
                }
            }

            return list
                .OrderBy(x => x.StartMs)
                .ThenBy(x => x.Phase)
                .ToArray();
        }

        private static ZwavSagComputedEventDto[] BuildComputedEventsFromPhases(
            ZwavSagDetailDto detail,
            ZwavSagProcessPhaseDto[] phases,
            DateTime waveStartTimeUtc,
            decimal interruptThresholdPct)
        {
            if (phases == null || phases.Length == 0)
                return Array.Empty<ZwavSagComputedEventDto>();

            var list = new List<ZwavSagComputedEventDto>(phases.Length);

            for (int i = 0; i < phases.Length; i++)
            {
                var p = phases[i];
                if (p == null)
                    continue;

                double startMs = (p.StartTimeUtc - waveStartTimeUtc).TotalMilliseconds;
                double endMs = (p.EndTimeUtc - waveStartTimeUtc).TotalMilliseconds;
                decimal worstPct = p.ResidualVoltagePct;
                string eventType = worstPct <= interruptThresholdPct ? "Interruption" : "Sag";
                decimal sagMagnitude = decimal.Round(100m - worstPct, 3);

                list.Add(new ZwavSagComputedEventDto
                {
                    EventType = eventType,
                    ChannelIndex = p.ChannelIndex,
                    GroupName = p.GroupName,
                    ChannelName = p.ChannelName,
                    Phase = p.Phase,
                    OccurTimeUtc = ResolvePhaseOccurTimeUtc(detail, p),
                    StartTimeUtc = p.StartTimeUtc,
                    EndTimeUtc = p.EndTimeUtc,
                    StartMs = startMs,
                    EndMs = endMs,
                    DurationMs = p.DurationMs,
                    ResidualVoltage = p.ResidualVoltage,
                    ResidualVoltagePct = decimal.Round(worstPct, 3),
                    SagMagnitudePct = sagMagnitude
                });
            }

            return list
                .OrderBy(x => x.StartMs)
                .ThenBy(x => x.Phase)
                .ToArray();
        }

        private static DateTime ResolvePhaseOccurTimeUtc(
            ZwavSagDetailDto detail,
            ZwavSagProcessPhaseDto phase)
        {
            if (phase?.IsWorstPhase == true && detail?.OccurTimeUtc.HasValue == true)
                return detail.OccurTimeUtc.Value;

            return phase?.StartTimeUtc ?? detail?.OccurTimeUtc ?? DateTime.MinValue;
        }

        private static ZwavSagMarkerDto[] BuildMarkers(
            ZwavSagDetailDto detail,
            ZwavSagProcessPhaseDto[] phases,
            DateTime waveStartTimeUtc,
            List<ZwavSagRmsPointDto> rmsPoints,
            ZwavSagComputedEventDto[] computedEvents = null)
        {
            var list = new List<ZwavSagMarkerDto>();

            if (computedEvents != null && computedEvents.Length > 0)
            {
                foreach (var evt in computedEvents)
                {
                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "EventStart",
                        Phase = evt.Phase,
                        TimeMs = evt.StartMs,
                        Label = $"{evt.Phase} 开始"
                    });

                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "EventEnd",
                        Phase = evt.Phase,
                        TimeMs = evt.EndMs,
                        Label = $"{evt.Phase} 结束"
                    });
                }
            }
            else if (detail != null)
            {
                if (detail.StartTimeUtc.HasValue)
                {
                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "EventStart",
                        Phase = detail.TriggerPhase,
                        TimeMs = (detail.StartTimeUtc.Value - waveStartTimeUtc).TotalMilliseconds,
                        Label = "事件开始"
                    });
                }

                if (detail.EndTimeUtc.HasValue)
                {
                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "EventEnd",
                        Phase = detail.EndPhase,
                        TimeMs = (detail.EndTimeUtc.Value - waveStartTimeUtc).TotalMilliseconds,
                        Label = "事件结束"
                    });
                }
            }

            if (phases != null && phases.Length > 0)
            {
                foreach (var p in phases)
                {
                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "PhaseStart",
                        Phase = p.Phase,
                        TimeMs = (p.StartTimeUtc - waveStartTimeUtc).TotalMilliseconds,
                        Label = $"{p.Phase} 相开始"
                    });

                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "PhaseEnd",
                        Phase = p.Phase,
                        TimeMs = (p.EndTimeUtc - waveStartTimeUtc).TotalMilliseconds,
                        Label = $"{p.Phase} 相结束"
                    });
                }
            }

            if (rmsPoints != null && rmsPoints.Count > 0)
            {
                IEnumerable<ZwavSagRmsPointDto> scope = rmsPoints.Where(x => x != null);
                var worst = scope
                    .OrderBy(x => x.RmsPct)
                    .FirstOrDefault();

                if (worst != null)
                {
                    list.Add(new ZwavSagMarkerDto
                    {
                        Kind = "Worst",
                        Phase = worst.Phase,
                        TimeMs = worst.TimeMs,
                        Label = "最小RMS%",
                        Value = worst.RmsPct
                    });
                }
            }

            return list
                .Where(x => x != null)
                .Where(x => !double.IsNaN(x.TimeMs) && !double.IsInfinity(x.TimeMs))
                .OrderBy(x => x.TimeMs)
                .ToArray();
        }

        private static (int? fromSample, int? toSample) SuggestSampleRange(
            ZwavSagComputedEventDto[] computedEvents,
            ZwavSagRmsPointDto[] rmsPoints)
        {
            // 保留方法，兼容旧调用，但不再用于默认过程展示
            if (rmsPoints == null || rmsPoints.Length == 0)
                return (null, null);

            var valid = rmsPoints.Where(x => x != null).ToList();
            if (valid.Count == 0)
                return (null, null);

            return (valid.Min(x => x.SampleNo), valid.Max(x => x.SampleNo));
        }

        /// <summary>
        /// 从前端请求中解析出本次需要处理的录波文件 ID 集合。
        /// 支持直接传 FileIds，也支持先传 AnalysisGuid 再反查 FileId。
        /// </summary>
        private async Task<List<int>> ResolveFileIdsAsync(AnalyzeZwavSagRequest req)
        {
            var fileIds = new HashSet<int>();

            if (req.FileIds != null && req.FileIds.Length > 0)
            {
                // 显式传入的 FileIds 直接纳入处理集合。
                for (int i = 0; i < req.FileIds.Length; i++)
                {
                    if (req.FileIds[i] > 0)
                        fileIds.Add(req.FileIds[i]);
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
                    // AnalysisGuid 最终也要还原到文件维度，便于后续统一判断是否已在处理中。
                    var analysisFileIds = await _context.ZwavAnalyses
                        .AsNoTracking()
                        .Where(x => guids.Contains(x.AnalysisGuid))
                        .Select(x => x.FileId)
                        .Distinct()
                        .ToListAsync()
                        .ConfigureAwait(false);

                    for (int i = 0; i < analysisFileIds.Count; i++)
                    {
                        if (analysisFileIds[i] > 0)
                            fileIds.Add(analysisFileIds[i]);
                    }
                }
            }

            return fileIds.ToList();
        }

        /// <summary>
        /// 构建分析器可直接消费的暂降分析上下文。
        /// 这里负责把数据库中的解析产物和前端传入参数整合成纯计算对象。
        /// </summary>
        private async Task<ZwavSagAnalyzeContext> BuildAnalyzeContextAsync(
            ZwavAnalysis analysis,
            AnalyzeZwavSagRequest req)
        {
            if (analysis == null)
                throw new ArgumentNullException(nameof(analysis));

            int analysisId = analysis.Id;

            // cfg/hdr 提供采样频率、时间倍率、故障起始时间等辅助元数据。
            var cfg = await _context.ZwavCfgs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == analysisId)
                .ConfigureAwait(false);

            var hdr = await _context.ZwavHdrs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == analysisId)
                .ConfigureAwait(false);

            var voltageChannels = await BuildVoltageChannelsAsync(analysisId).ConfigureAwait(false);
            if (voltageChannels.Length == 0)
                throw new InvalidOperationException("未识别到电压通道，无法进行暂降分析");

            // 原始波形行数据是后续生成采样点、时间轴和单通道序列的基础输入。
            var rawRows = await LoadWaveRowsAsync(analysisId).ConfigureAwait(false);
            if (rawRows.Count == 0)
                throw new InvalidOperationException("未读取到波形采样数据");

            decimal frequencyHz = (cfg?.FrequencyHz).GetValueOrDefault() > 0 ? cfg.FrequencyHz.Value : 50m;
            decimal timeMul = (cfg?.TimeMul).GetValueOrDefault() > 0 ? cfg.TimeMul.Value : 0.001m;

            var sampleBuildResult = BuildSamplesAndSeries(rawRows, voltageChannels, timeMul);
            if (sampleBuildResult.Samples.Count == 0)
                throw new InvalidOperationException("采样点构建失败");

            // 这里完成“数据库结构 -> 分析器上下文”的转换，后续分析器只关注计算，不关心存储来源。
            var ctx = new ZwavSagAnalyzeContext
            {
                AnalysisId = analysisId,
                FrequencyHz = frequencyHz,
                TimeMul = timeMul,
                WaveStartTimeUtc = ResolveWaveStartTimeUtc(analysis, cfg),
                TriggerTimeUtc = ResolveTriggerTimeUtc(analysis, cfg),
                FaultStartTime = TryParseFaultStartTime(hdr?.FaultStartTime, out var parsedFaultStartTime) ? parsedFaultStartTime : (DateTime?)null,
                ReferenceType = NormalizeReferenceType(req.ReferenceType, req.ReferenceVoltage),
                ReferenceVoltage = req.ReferenceVoltage,
                RecoverThresholdPct = req.RecoverThresholdPct,
                SagThresholdPct = req.SagThresholdPct > 0 ? req.SagThresholdPct : 90m,
                InterruptThresholdPct = req.InterruptThresholdPct > 0 ? req.InterruptThresholdPct : 10m,
                HysteresisPct = req.HysteresisPct >= 0 ? req.HysteresisPct : 2m,
                MinDurationMs = req.MinDurationMs >= 0 ? req.MinDurationMs : 10m,
                RmsMode = "Fixed-OneCycle-HalfCycleStep",
                VoltageChannels = voltageChannels,
                Samples = sampleBuildResult.Samples,
                TimeAxisMs = sampleBuildResult.TimeAxisMs,
                ChannelSeriesMap = sampleBuildResult.ChannelSeriesMap,
                PhaseRules = await ZwavSagVoltageChannelRuleMatcher.LoadRulesAsync(_context).ConfigureAwait(false),
            };

            ctx.AnalyzableSegments = BuildAnalyzableSegments(ctx);

            LogAnalyzeContext(ctx);
            return ctx;
        }

        /// <summary>
        /// 从解析得到的模拟量通道里筛选出可参与暂降分析的电压通道，并补齐相别和分组信息。
        /// </summary>
        private async Task<ZwavVoltageChannelContext[]> BuildVoltageChannelsAsync(int analysisId)
        {
            var rules = await ZwavSagVoltageChannelRuleMatcher.LoadRulesAsync(_context).ConfigureAwait(false);
            var groupRules = await ZwavSagVoltageChannelRuleMatcher.LoadGroupRulesAsync(_context).ConfigureAwait(false);

            var channels = await _context.ZwavChannels
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId && x.IsEnable == 1 && x.ChannelType == "Analog")
                .OrderBy(x => x.ChannelIndex)
                .Select(x => new
                {
                    x.ChannelIndex,
                    x.ChannelCode,
                    x.ChannelName,
                    x.Unit
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var result = new List<ZwavVoltageChannelContext>(channels.Count);

            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                var channelName = (ch.ChannelName ?? string.Empty).Trim();
                var channelCode = (ch.ChannelCode ?? string.Empty).Trim();
                var unit = (ch.Unit ?? string.Empty).Trim();

                // 先过滤掉明显不是电压量的通道，减少后续误识别。
                if (!ZwavSagVoltageChannelRuleMatcher.IsVoltageChannel(channelName, channelCode, unit))
                    continue;

                // 相别识别不到时无法参与后续排序和事件归类，因此直接跳过。
                var phase = ZwavSagVoltageChannelRuleMatcher.MatchPhase(channelName, channelCode, unit, rules);
                if (string.IsNullOrWhiteSpace(phase))
                    continue;

                // 分组优先走规则表；没有命中时回退到 CH-序号，保证前端仍可展示该通道。
                var groupName = ZwavSagVoltageChannelRuleMatcher.MatchGroup(channelName, channelCode, unit, groupRules);
                if (string.IsNullOrWhiteSpace(groupName))
                    groupName = $"CH-{ch.ChannelIndex}";

                result.Add(new ZwavVoltageChannelContext
                {
                    ChannelIndex = ch.ChannelIndex,
                    GroupName = groupName,
                    Phase = phase,
                    ChannelCode = channelCode,
                    ChannelName = channelName,
                    Unit = unit
                });
            }

            return result.ToArray();
        }

        /// <summary>
        /// 加载指定分析记录对应的全部录波行数据，并保持样本号升序。
        /// </summary>
        private async Task<List<WaveRowRaw>> LoadWaveRowsAsync(int analysisId)
        {
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
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 把逐行录波数据转换成分析器需要的采样点集合、时间轴以及按通道索引的原始序列映射。
        /// </summary>
        private SampleBuildResult BuildSamplesAndSeries(
            List<WaveRowRaw> rawRows,
            ZwavVoltageChannelContext[] voltageChannels,
            decimal timeMul)
        {
            var result = new SampleBuildResult();

            if (rawRows == null || rawRows.Count == 0)
                return result;

            if (voltageChannels == null || voltageChannels.Length == 0)
                return result;

            var channelIndexes = voltageChannels
                .Select(x => x.ChannelIndex)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            int rowCount = rawRows.Count;
            var timeAxisMs = new double[rowCount];
            var channelSeriesMap = new Dictionary<int, double?[]>(channelIndexes.Length);

            for (int i = 0; i < channelIndexes.Length; i++)
                channelSeriesMap[channelIndexes[i]] = new double?[rowCount];

            var samples = new List<ZwavSagSamplePoint>(rowCount);

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = rawRows[rowIndex];

                // 优先使用已解析好的毫秒时间；缺失时再根据原始时间值和倍率推导。
                double timeMs = row.TimeMs;
                if (timeMs <= 0 && row.TimeRaw > 0)
                {
                    if (timeMul > 0)
                        timeMs = SafeTimeMsFromRaw(row.TimeRaw, timeMul);
                    else
                        timeMs = row.TimeRaw / 1000.0;
                }

                timeAxisMs[rowIndex] = timeMs;

                var values = new Dictionary<int, double?>(channelIndexes.Length);

                for (int j = 0; j < channelIndexes.Length; j++)
                {
                    int channelIndex = channelIndexes[j];
                    var value = row.GetAnalogNullable(channelIndex);

                    // 同时保留“按通道取序列”和“按采样点取多通道值”两种结构，方便不同分析阶段访问。
                    channelSeriesMap[channelIndex][rowIndex] = value;
                    values[channelIndex] = value;
                }

                samples.Add(new ZwavSagSamplePoint
                {
                    SampleNo = row.SampleNo,
                    TimeRaw = row.TimeRaw,
                    TimeMs = timeMs,
                    ChannelValues = values
                });
            }

            result.Samples = samples;
            result.TimeAxisMs = timeAxisMs;
            result.ChannelSeriesMap = channelSeriesMap;

            return result;
        }

        /// <summary>
        /// 按采样间隔变化切分可分析区段。
        /// 仅保留采样密度足够支撑周波 RMS 计算的片段，避免低密度数据干扰暂降识别。
        /// </summary>
        private List<ZwavSampleSegmentInfo> BuildAnalyzableSegments(ZwavSagAnalyzeContext ctx)
        {
            if (ctx == null || ctx.Samples == null || ctx.Samples.Count < 2)
                return new List<ZwavSampleSegmentInfo>();

            var rawSegments = SplitBySampleIntervalForService(ctx.Samples, ctx.FrequencyHz);
            var result = new List<ZwavSampleSegmentInfo>(rawSegments.Count);

            for (int i = 0; i < rawSegments.Count; i++)
            {
                var seg = rawSegments[i];
                double sampleRate = seg.SampleIntervalMs > 0 ? 1000d / seg.SampleIntervalMs : 0d;
                int samplesPerCycle = sampleRate > 0
                    ? Math.Max(1, (int)Math.Round(sampleRate / (double)ctx.FrequencyHz))
                    : 1;

                // 小于 8 点/周波时，RMS 估算会明显失真，宁可舍弃该片段也不要硬算。
                if (samplesPerCycle >= 8)
                {
                    result.Add(new ZwavSampleSegmentInfo
                    {
                        StartIndex = seg.StartIndex,
                        EndIndex = seg.EndIndex,
                        SampleIntervalMs = seg.SampleIntervalMs
                    });
                }
            }

            return result;
        }

        private List<ZwavSampleSegmentInfo> SplitBySampleIntervalForService(
            IReadOnlyList<ZwavSagSamplePoint> samples,
            decimal frequencyHz)
        {
            var result = new List<ZwavSampleSegmentInfo>();
            if (samples == null || samples.Count < 2)
                return result;

            double cycleMs = 1000d / (double)frequencyHz;

            int segStart = 0;
            double prevDiff = SafePositiveDiffForService(samples[1].TimeMs - samples[0].TimeMs);
            if (prevDiff <= 0d)
                prevDiff = cycleMs / 24d;

            for (int i = 2; i < samples.Count; i++)
            {
                double diff = SafePositiveDiffForService(samples[i].TimeMs - samples[i - 1].TimeMs);
                if (diff <= 0d)
                    continue;

                bool changed =
                    diff > prevDiff * 1.8d ||
                    diff < prevDiff / 1.8d ||
                    Math.Abs(diff - prevDiff) > Math.Max(0.2d, prevDiff * 0.35d);

                if (changed)
                {
                    result.Add(new ZwavSampleSegmentInfo
                    {
                        StartIndex = segStart,
                        EndIndex = i - 1,
                        SampleIntervalMs = prevDiff
                    });

                    segStart = i - 1;
                    prevDiff = diff;
                }
                else
                {
                    prevDiff = (prevDiff + diff) / 2d;
                }
            }

            result.Add(new ZwavSampleSegmentInfo
            {
                StartIndex = segStart,
                EndIndex = samples.Count - 1,
                SampleIntervalMs = prevDiff
            });

            return result
                .Where(x => x.EndIndex > x.StartIndex)
                .ToList();
        }

        private double SafePositiveDiffForService(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value)
                ? value
                : 0d;
        }

        private void LogAnalyzeContext(ZwavSagAnalyzeContext ctx)
        {
            _logger.LogInformation("[Sag] AnalysisId={AnalysisId}", ctx.AnalysisId);
            _logger.LogInformation("[Sag] FrequencyHz={FrequencyHz}, TimeMul={TimeMul}", ctx.FrequencyHz, ctx.TimeMul);
            _logger.LogInformation("[Sag] VoltageChannels={VoltageChannels}",
                string.Join(", ", ctx.VoltageChannels.Select(x => $"{x.ChannelIndex}:{x.GroupName}:{x.ChannelName}:{x.Phase}")));
            _logger.LogInformation("[Sag] Samples={SampleCount}", ctx.Samples.Count);
            _logger.LogInformation("[Sag] ChannelSeriesCount={ChannelSeriesCount}", ctx.ChannelSeriesMap?.Count ?? 0);
            _logger.LogInformation("[Sag] AnalyzableSegments={SegmentCount}", ctx.AnalyzableSegments?.Count ?? 0);

            if (ctx.Samples.Count >= 2)
            {
                _logger.LogInformation("[Sag] FirstSample TimeRaw={TimeRaw1}, TimeMs={TimeMs1}",
                    ctx.Samples[0].TimeRaw, ctx.Samples[0].TimeMs);
                _logger.LogInformation("[Sag] SecondSample TimeRaw={TimeRaw2}, TimeMs={TimeMs2}",
                    ctx.Samples[1].TimeRaw, ctx.Samples[1].TimeMs);
                _logger.LogInformation("[Sag] SampleIntervalMs={SampleIntervalMs}",
                    ctx.Samples[1].TimeMs - ctx.Samples[0].TimeMs);
            }
        }

        public async Task<ZwavSagDetailDto> GetDetailAsync(int id)
        {
            var entity = await _context.ZwavSagEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id)
                .ConfigureAwait(false);

            return entity == null ? null : MapDetail(entity);
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
                    ChannelIndex = x.ChannelIndex,
                    GroupName = x.GroupName,
                    ChannelName = x.ChannelName,
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
                .ToArrayAsync()
                .ConfigureAwait(false);
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
                    Progress = x.Progress,
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
                .ToArrayAsync()
                .ConfigureAwait(false);
        }

        public async Task<bool> UpdateAsync(int id, UpdateZwavSagEventRequest req)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));

            var entity = await _context.ZwavSagEvents
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);

            if (entity == null)
                return false;

            entity.Remark = req.Remark;
            entity.UpdTime = DateTime.UtcNow;

            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.ZwavSagEvents
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);

            if (entity == null)
                return false;

            var phases = await _context.ZwavSagEventPhases
                .Where(x => x.SagEventId == id)
                .ToListAsync()
                .ConfigureAwait(false);

            if (phases.Count > 0)
                _context.ZwavSagEventPhases.RemoveRange(phases);

            var rmsPoints = await _context.ZwavSagRmsPoints
                .Where(x => x.SagEventId == id)
                .ToListAsync()
                .ConfigureAwait(false);

            if (rmsPoints.Count > 0)
                _context.ZwavSagRmsPoints.RemoveRange(rmsPoints);

            _context.ZwavSagEvents.Remove(entity);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<int> DeleteByFileIdAsync(int fileId)
        {
            var deletedCount = await DeleteByFileIdInternalAsync(fileId).ConfigureAwait(false);
            if (deletedCount > 0)
                await _context.SaveChangesAsync().ConfigureAwait(false);

            return deletedCount;
        }

        #region 私有辅助方法

        private async Task<int> DeleteByFileIdInternalAsync(int fileId)
        {
            var events = await _context.ZwavSagEvents
                .Where(x => x.FileId == fileId)
                .ToListAsync()
                .ConfigureAwait(false);

            if (events.Count == 0)
                return 0;

            var eventIds = events.Select(x => x.Id).ToList();

            var phases = await _context.ZwavSagEventPhases
                .Where(x => eventIds.Contains(x.SagEventId))
                .ToListAsync()
                .ConfigureAwait(false);

            if (phases.Count > 0)
                _context.ZwavSagEventPhases.RemoveRange(phases);

            var rmsPoints = await _context.ZwavSagRmsPoints
                .Where(x => eventIds.Contains(x.SagEventId))
                .ToListAsync()
                .ConfigureAwait(false);

            if (rmsPoints.Count > 0)
                _context.ZwavSagRmsPoints.RemoveRange(rmsPoints);

            _context.ZwavSagEvents.RemoveRange(events);

            _logger.LogInformation(
                "删除旧暂降结果完成，FileId={FileId}, EventCount={EventCount}, PhaseCount={PhaseCount}, RmsCount={RmsCount}",
                fileId,
                events.Count,
                phases.Count,
                rmsPoints.Count);

            return events.Count;
        }

        private async Task<ZwavSagEvent> CreateProcessingEventAsync(int fileId, string originalName, DateTime startAt)
        {
            var now = DateTime.UtcNow;
            var entity = new ZwavSagEvent
            {
                FileId = fileId,
                OriginalName = originalName,
                Status = 1,
                Progress = 0,
                ErrorMessage = null,
                HasSag = false,
                EventType = "Processing",
                EventCount = 0,
                StartTime = startAt,
                FinishTime = null,
                CostMs = null,
                RawEventCount = 0,
                SeqNo = 0,
                Remark = null,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavSagEvents.Add(entity);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return entity;
        }

        private async Task UpdateProgressAsync(int sagEventId, int progress)
        {
            if (sagEventId <= 0)
                return;

            var entity = _context.ZwavSagEvents.Local.FirstOrDefault(x => x.Id == sagEventId);
            if (entity == null)
            {
                entity = await _context.ZwavSagEvents
                    .FirstOrDefaultAsync(x => x.Id == sagEventId)
                    .ConfigureAwait(false);
            }

            if (entity == null)
                return;

            entity.Progress = ClampProgress(progress);
            entity.UpdTime = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        private async Task MarkProcessingEventFailedAsync(int sagEventId, string errorMessage)
        {
            if (sagEventId <= 0)
                return;

            var entity = _context.ZwavSagEvents.Local.FirstOrDefault(x => x.Id == sagEventId);
            if (entity == null)
            {
                entity = await _context.ZwavSagEvents
                    .FirstOrDefaultAsync(x => x.Id == sagEventId)
                    .ConfigureAwait(false);
            }

            if (entity == null)
                return;

            var finishAt = DateTime.UtcNow;
            entity.Status = 3;
            entity.Progress = 100;
            entity.ErrorMessage = errorMessage;
            entity.EventType = "Failed";
            entity.FinishTime = finishAt;
            entity.UpdTime = finishAt;

            if (entity.StartTime.HasValue)
                entity.CostMs = (long)Math.Round((finishAt - entity.StartTime.Value).TotalMilliseconds);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        private static AnalyzeZwavSagRequest CloneAnalyzeRequest(AnalyzeZwavSagRequest req)
        {
            if (req == null)
                return null;

            return new AnalyzeZwavSagRequest
            {
                FileIds = req.FileIds?.Where(x => x > 0).Distinct().ToArray(),
                AnalysisGuids = req.AnalysisGuids?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ReferenceType = req.ReferenceType,
                ReferenceVoltage = req.ReferenceVoltage,
                SagThresholdPct = req.SagThresholdPct,
                RecoverThresholdPct = req.RecoverThresholdPct,
                InterruptThresholdPct = req.InterruptThresholdPct,
                HysteresisPct = req.HysteresisPct,
                MinDurationMs = req.MinDurationMs,
                ForceRebuild = req.ForceRebuild
            };
        }

        private static int ClampProgress(int progress)
        {
            if (progress < 0) return 0;
            if (progress > 100) return 100;
            return progress;
        }

        private List<PersistPackage> BuildPersistPackages(
            int fileId,
            string originalName,
            DateTime startAt,
            DateTime finishAt,
            long costMs,
            ZwavSagAnalyzeContext analyzeContext,
            ZwavSagAnalyzeResult analyzeResult)
        {
            var result = new List<PersistPackage>();
            var rmsPoints = analyzeResult?.RmsPoints ?? new List<ZwavSagRmsPointResult>();
            var events = analyzeResult?.Events?.Where(x => x != null).ToList() ?? new List<ZwavSagEventResult>();

            if (events.Count == 0)
            {
                result.Add(new PersistPackage
                {
                    Event = CreateNormalEvent(fileId, originalName, startAt, finishAt, costMs),
                    Phases = new List<ZwavSagEventPhase>(),
                    RmsPoints = BuildRmsEntities(0, rmsPoints, finishAt)
                });

                return result;
            }

            // 这里仍保留“一个文件生成一条主记录”的持久化方式，
            // 但主记录内容取最严重事件；所有相明细与 RMS 统一挂到该记录下。
            var primaryEvent = SelectPrimaryEvent(events);

            var allPhases = events
                .SelectMany(x => x?.Phases ?? Enumerable.Empty<ZwavSagEventPhaseResult>())
                .Where(x => x != null)
                .OrderBy(x => x.StartTimeUtc)
                .ThenBy(x => x.Phase)
                .ThenBy(x => x.ChannelIndex)
                .ToList();

            var primaryEventPhases = primaryEvent.Phases?
                .Where(x => x != null)
                .ToList() ?? new List<ZwavSagEventPhaseResult>();
            var primaryPhase = SelectPrimaryPhase(primaryEventPhases) ?? SelectPrimaryPhase(allPhases);

            result.Add(new PersistPackage
            {
                Event = CreateSagEventEntity(fileId, originalName, startAt, finishAt, costMs, primaryEvent, primaryPhase, 1, events, analyzeContext),
                Phases = BuildPhaseEntities(0, allPhases, finishAt, analyzeContext, primaryPhase),
                RmsPoints = BuildRmsEntities(0, rmsPoints, finishAt)
            });

            return result;
        }

        private static ZwavSagEventResult SelectPrimaryEvent(List<ZwavSagEventResult> events)
        {
            if (events == null || events.Count == 0)
                throw new ArgumentException("events 不能为空", nameof(events));

            var candidates = events.Where(x => x != null).ToList();
            if (candidates.Count == 0)
                throw new ArgumentException("events 中没有有效事件", nameof(events));

            return candidates
                .Select(x => new
                {
                    Event = x,
                    Phase = SelectPrimaryPhase(x.Phases?.Where(p => p != null).ToList())
                })
                .OrderByDescending(x => x.Phase?.SagPercent ?? x.Event.SagPercent)
                .ThenBy(x => x.Phase?.ResidualVoltagePct ?? x.Event.ResidualVoltagePct)
                .ThenByDescending(x => x.Phase?.DurationMs ?? x.Event.DurationMs)
                .ThenBy(x => x.Phase?.StartTimeUtc ?? x.Event.StartTimeUtc)
                .First()
                .Event;
        }

        private static ZwavSagEventPhaseResult SelectPrimaryPhase(List<ZwavSagEventPhaseResult> phases)
        {
            if (phases == null || phases.Count == 0)
                return null;

            return phases
                .Where(x => x != null)
                .OrderByDescending(x => x.SagPercent)
                .ThenByDescending(x => x.DurationMs)
                .ThenBy(x => x.ResidualVoltagePct)
                .ThenBy(x => x.StartTimeUtc)
                .ThenBy(x => x.Phase)
                .FirstOrDefault();
        }

        private static ZwavSagDetailDto MapDetail(ZwavSagEvent x)
        {
            return new ZwavSagDetailDto
            {
                Id = x.Id,
                FileId = x.FileId,
                OriginalName = x.OriginalName,
                Status = x.Status,
                Progress = x.Progress,
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

        private static ZwavSagEvent CreateNormalEvent(int fileId, string originalName, DateTime startAt, DateTime finishAt, long costMs)
        {
            return new ZwavSagEvent
            {
                FileId = fileId,
                OriginalName = originalName,
                Status = 2,
                Progress = 100,
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
        }

        private static ZwavSagEvent CreateFailedEvent(int fileId, string originalName, DateTime startAt, DateTime failAt, string errorMessage)
        {
            return new ZwavSagEvent
            {
                FileId = fileId,
                OriginalName = originalName,
                Status = 3,
                Progress = 100,
                ErrorMessage = errorMessage,
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
            };
        }

        private static ZwavSagEvent CreateSagEventEntity(
            int fileId,
            string originalName,
            DateTime startAt,
            DateTime finishAt,
            long costMs,
            ZwavSagEventResult evt,
            ZwavSagEventPhaseResult primaryPhase,
            int seqNo,
            IReadOnlyCollection<ZwavSagEventResult> allEvents,
            ZwavSagAnalyzeContext analyzeContext)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            int eventCount = allEvents?.Count(x => x != null) ?? 1;
            int rawEventCount = allEvents == null
                ? Math.Max(1, evt.RawEventCount)
                : allEvents
                    .Where(x => x != null)
                    .Sum(x => Math.Max(1, x.RawEventCount > 0 ? x.RawEventCount : 1));

            return new ZwavSagEvent
            {
                FileId = fileId,
                OriginalName = originalName,
                Status = 2,
                Progress = 100,
                ErrorMessage = null,
                HasSag = true,
                EventType = evt.EventType,
                EventCount = Math.Max(1, eventCount),
                StartTime = startAt,
                FinishTime = finishAt,
                CostMs = costMs,
                StartTimeUtc = ResolvePersistedTimeUtc(evt.StartTimeUtc, analyzeContext),
                EndTimeUtc = ResolvePersistedTimeUtc(evt.EndTimeUtc, analyzeContext),
                OccurTimeUtc = ResolvePersistedTimeUtc(evt.OccurTimeUtc, analyzeContext),
                DurationMs = ClampDuration(evt.DurationMs),
                TriggerPhase = evt.TriggerPhase,
                EndPhase = evt.EndPhase,
                WorstPhase = evt.WorstPhase,
                ReferenceType = evt.ReferenceType,
                ReferenceVoltage = ClampVoltage(evt.ReferenceVoltage),
                ResidualVoltage = ClampVoltage(evt.ResidualVoltage),
                ResidualVoltagePct = ClampPercent(evt.ResidualVoltagePct),
                SagDepth = ClampVoltage(evt.SagDepth),
                SagPercent = ClampPercent(evt.SagPercent),
                StartAngleDeg = ClampAngle(evt.StartAngleDeg),
                PhaseJumpDeg = ClampAngle(evt.PhaseJumpDeg),
                SagThresholdPct = ClampPercent(evt.SagThresholdPct),
                InterruptThresholdPct = ClampPercent(evt.InterruptThresholdPct),
                HysteresisPct = ClampPercent(evt.HysteresisPct),
                IsMergedStatEvent = evt.IsMergedStatEvent,
                MergeGroupId = evt.IsMergedStatEvent ? Guid.NewGuid().ToString("N") : null,
                RawEventCount = Math.Max(1, rawEventCount),
                SeqNo = seqNo,
                Remark = null,
                CrtTime = finishAt,
                UpdTime = finishAt
            };
        }

        private static DateTime ResolvePersistedTimeUtc(
            DateTime timeUtc,
            ZwavSagAnalyzeContext analyzeContext)
        {
            return timeUtc;
        }

        private static List<ZwavSagEventPhase> BuildPhaseEntities(
            int sagEventId,
            List<ZwavSagEventPhaseResult> phases,
            DateTime finishAt,
            ZwavSagAnalyzeContext analyzeContext,
            ZwavSagEventPhaseResult primaryPhase)
        {
            if (phases == null || phases.Count == 0)
                return new List<ZwavSagEventPhase>();

            int seq = 1;
            var entities = new List<ZwavSagEventPhase>(phases.Count);

            for (int i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                entities.Add(new ZwavSagEventPhase
                {
                    SagEventId = sagEventId,
                    SeqNo = seq++,
                    ChannelIndex = phase.ChannelIndex > 0 ? phase.ChannelIndex : (int?)null,
                    GroupName = string.IsNullOrWhiteSpace(phase.GroupName) ? null : phase.GroupName.Trim(),
                    ChannelName = string.IsNullOrWhiteSpace(phase.ChannelName) ? null : phase.ChannelName.Trim(),
                    Phase = phase.Phase,
                    StartTimeUtc = ResolvePersistedTimeUtc(phase.StartTimeUtc, analyzeContext),
                    EndTimeUtc = ResolvePersistedTimeUtc(phase.EndTimeUtc, analyzeContext),
                    DurationMs = ClampDuration(phase.DurationMs).GetValueOrDefault(),
                    ReferenceType = phase.ReferenceType,
                    ReferenceVoltage = ClampVoltage(phase.ReferenceVoltage).GetValueOrDefault(),
                    ResidualVoltage = ClampVoltage(phase.ResidualVoltage).GetValueOrDefault(),
                    ResidualVoltagePct = ClampPercent(phase.ResidualVoltagePct).GetValueOrDefault(),
                    SagDepth = ClampVoltage(phase.SagDepth).GetValueOrDefault(),
                    SagPercent = ClampPercent(phase.SagPercent).GetValueOrDefault(),
                    StartAngleDeg = ClampAngle(phase.StartAngleDeg),
                    PhaseJumpDeg = ClampAngle(phase.PhaseJumpDeg),
                    SagThresholdPct = ClampPercent(phase.SagThresholdPct),
                    InterruptThresholdPct = ClampPercent(phase.InterruptThresholdPct),
                    HysteresisPct = ClampPercent(phase.HysteresisPct),
                    IsTriggerPhase = phase.IsTriggerPhase,
                    IsEndPhase = phase.IsEndPhase,
                    IsWorstPhase = IsSelectedPrimaryPhase(phase, primaryPhase),
                    CrtTime = finishAt,
                    UpdTime = finishAt
                });
            }

            return entities;
        }

        private static bool IsSelectedPrimaryPhase(
            ZwavSagEventPhaseResult phase,
            ZwavSagEventPhaseResult primaryPhase)
        {
            if (phase == null)
                return false;

            if (primaryPhase == null)
                return phase.IsWorstPhase;

            if (ReferenceEquals(phase, primaryPhase))
                return true;

            return phase.ChannelIndex == primaryPhase.ChannelIndex
                && string.Equals(phase.Phase, primaryPhase.Phase, StringComparison.OrdinalIgnoreCase)
                && phase.StartTimeUtc == primaryPhase.StartTimeUtc
                && phase.EndTimeUtc == primaryPhase.EndTimeUtc
                && phase.ResidualVoltagePct == primaryPhase.ResidualVoltagePct;
        }

        private static List<ZwavSagRmsPoint> BuildRmsEntities(
            int sagEventId,
            List<ZwavSagRmsPointResult> rmsPoints,
            DateTime finishAt)
        {
            if (rmsPoints == null || rmsPoints.Count == 0)
                return new List<ZwavSagRmsPoint>();

            var entities = new List<ZwavSagRmsPoint>(rmsPoints.Count);

            for (int i = 0; i < rmsPoints.Count; i++)
            {
                var p = rmsPoints[i];
                entities.Add(new ZwavSagRmsPoint
                {
                    SagEventId = sagEventId,
                    ChannelIndex = p.ChannelIndex,
                    Phase = p.Phase,
                    SampleNo = p.SampleNo,
                    TimeMs = p.TimeMs,
                    Rms = ClampVoltage(p.Rms).GetValueOrDefault(),
                    RmsPct = ClampPercent(p.RmsPct).GetValueOrDefault(),
                    ReferenceVoltage = ClampVoltage(p.ReferenceVoltage).GetValueOrDefault(),
                    SeqNo = p.SeqNo,
                    CrtTime = finishAt,
                    UpdTime = finishAt
                });
            }

            return entities;
        }

        private static double SafeTimeMsFromRaw(long timeRaw, decimal timeMul)
        {
            if (timeRaw <= 0)
                return 0d;

            if (timeMul <= 0m)
                return timeRaw / 1000.0;

            double result = timeRaw * (double)timeMul;
            if (!double.IsNaN(result) && !double.IsInfinity(result))
                return result;

            return timeRaw / 1000.0;
        }

        private static decimal? ClampVoltage(decimal? value)
        {
            return ClampDecimal(value, MaxVoltageDbValue, 6);
        }

        private static decimal? ClampPercent(decimal? value)
        {
            return ClampDecimal(value, MaxPercentDbValue, 3);
        }

        private static decimal? ClampDuration(decimal? value)
        {
            return ClampDecimal(value, MaxDurationDbValue, 3);
        }

        private static decimal? ClampAngle(decimal? value)
        {
            return ClampDecimal(value, MaxAngleDbValue, 6);
        }

        private static decimal? ClampDecimal(decimal? value, decimal maxAbsValue, int scale)
        {
            if (!value.HasValue)
                return null;

            decimal v = value.Value;
            if (v > maxAbsValue)
                v = maxAbsValue;
            else if (v < -maxAbsValue)
                v = -maxAbsValue;

            return decimal.Round(v, scale, MidpointRounding.AwayFromZero);
        }

        private sealed class SampleBuildResult
        {
            public List<ZwavSagSamplePoint> Samples { get; set; } = new List<ZwavSagSamplePoint>();
            public double[] TimeAxisMs { get; set; } = Array.Empty<double>();
            public Dictionary<int, double?[]> ChannelSeriesMap { get; set; }
                = new Dictionary<int, double?[]>();
        }

        private sealed class PersistPackage
        {
            public ZwavSagEvent Event { get; set; }
            public List<ZwavSagEventPhase> Phases { get; set; } = new List<ZwavSagEventPhase>();
            public List<ZwavSagRmsPoint> RmsPoints { get; set; } = new List<ZwavSagRmsPoint>();
        }

        #endregion
    }
}
