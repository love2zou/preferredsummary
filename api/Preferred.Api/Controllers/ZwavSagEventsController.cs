using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Preferred.Api.Services;
using Zwav.Application.Parsing;
using Zwav.Application.Sag;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 电压暂降事件控制器
    /// 与录波解析控制器解耦，专注于暂降分析结果的生成、查询与维护。
    /// 
    /// 当前暂降算法固定采用：
    /// 1）1周波 RMS 窗口
    /// 2）半周波更新
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ZwavSagEventsController : ControllerBase
    {
        private readonly IZwavSagEventService _svc;
        private readonly ApplicationDbContext _context;

        public ZwavSagEventsController(
            IZwavSagEventService svc,
            ApplicationDbContext context)
        {
            _svc = svc;
            _context = context;
        }

        /// <summary>
        /// 分页查询暂降事件列表
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ZwavSagListItemDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetListAsync(
            [FromQuery] string keyword,
            [FromQuery] string eventType,
            [FromQuery] string phase,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page <= 0)
                    return BadRequestApi("page 必须大于 0");

                if (pageSize <= 0 || pageSize > 200)
                    return BadRequestApi("pageSize 必须在 1~200 之间");

                if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
                    return BadRequestApi("fromUtc 不能大于 toUtc");

                var data = await _svc.QueryAsync(
                    keyword,
                    eventType,
                    phase,
                    fromUtc,
                    toUtc,
                    page,
                    pageSize);

                return Ok(new ApiResponse<PagedResult<ZwavSagListItemDto>>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerError("查询暂降列表失败", ex);
            }
        }

        /// <summary>
        /// 获取暂降事件详情
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDetailAsync([FromRoute] int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var data = await _svc.GetDetailAsync(id);
                if (data == null)
                    return NotFound(new ApiErrorResponse { Message = "事件不存在" });

                return Ok(new ApiResponse<ZwavSagDetailDto>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("查询暂降事件详情失败", ex);
            }
        }

        /// <summary>
        /// 获取暂降事件相别明细
        /// </summary>
        [HttpGet("{id:int}/phases")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagPhaseDto[]>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPhasesAsync([FromRoute] int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var detail = await _svc.GetDetailAsync(id);
                if (detail == null)
                    return NotFound(new ApiErrorResponse { Message = "事件不存在" });

                var data = await _svc.GetPhasesAsync(id);

                return Ok(new ApiResponse<ZwavSagPhaseDto[]>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("查询暂降相别明细失败", ex);
            }
        }

        /// <summary>
        /// 根据录波文件ID查询暂降结果
        /// </summary>
        [HttpGet("by-file/{fileId:int}")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagDetailDto[]>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetByFileAsync([FromRoute] int fileId)
        {
            try
            {
                if (fileId <= 0)
                    return BadRequestApi("fileId 必须大于 0");

                var data = await _svc.GetByFileIdAsync(fileId);

                return Ok(new ApiResponse<ZwavSagDetailDto[]>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("按录波文件查询暂降结果失败", ex);
            }
        }

        /// <summary>
        /// 发起电压暂降分析
        /// 当前算法固定采用：1周波 RMS 窗口 + 半周波更新。
        /// 控制器层不再暴露 RMS 模式切换。
        /// </summary>
        [HttpPost("analyze")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<AnalyzeZwavSagResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> AnalyzeAsync([FromBody] AnalyzeZwavSagRequest req)
        {
            return ExecuteAnalyzeAsync(req, "暂降分析完成");
        }

        /// <summary>
        /// 重新分析指定录波文件的暂降结果
        /// 当前算法固定采用：1周波 RMS 窗口 + 半周波更新。
        /// 一般由前端显式传 ForceRebuild = true。
        /// </summary>
        [HttpPost("reanalyze")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<AnalyzeZwavSagResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> ReAnalyzeAsync([FromBody] AnalyzeZwavSagRequest req)
        {
            if (req != null)
                req.ForceRebuild = true;

            return ExecuteAnalyzeAsync(req, "重新分析完成");
        }

        /// <summary>
        /// 更新暂降事件（仅建议用于备注等有限字段修正）
        /// </summary>
        [HttpPatch("{id:int}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAsync(
            [FromRoute] int id,
            [FromBody] UpdateZwavSagEventRequest req)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                if (req == null)
                    return BadRequestApi("请求体不能为空");

                var ok = await _svc.UpdateAsync(id, req);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "事件不存在" });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "更新成功",
                    Data = null
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerError("更新暂降事件失败", ex);
            }
        }

        /// <summary>
        /// 删除单个暂降事件
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var ok = await _svc.DeleteAsync(id);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "事件不存在" });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return ServerError("删除暂降事件失败", ex);
            }
        }

        /// <summary>
        /// 按录波文件删除全部暂降结果
        /// </summary>
        [HttpDelete("by-file/{fileId:int}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteByFileAsync([FromRoute] int fileId)
        {
            try
            {
                if (fileId <= 0)
                    return BadRequestApi("fileId 必须大于 0");

                var deletedCount = await _svc.DeleteByFileIdAsync(fileId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"删除成功，共删除 {deletedCount} 条事件",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return ServerError("按录波文件删除暂降结果失败", ex);
            }
        }

        [HttpGet("{id:int}/process")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagProcessDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProcessAsync([FromRoute] int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var detail = await _svc.GetDetailAsync(id);
                if (detail == null)
                    return NotFound(new ApiErrorResponse { Message = "事件不存在" });

                var phases = await _svc.GetPhasesAsync(id);

                var analysis = await _context.ZwavAnalyses
                    .AsNoTracking()
                    .Where(x => x.FileId == detail.FileId)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                if (analysis == null)
                    return NotFound(new ApiErrorResponse { Message = "未找到对应解析任务" });

                var cfg = await _context.ZwavCfgs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.AnalysisId == analysis.Id);

                decimal frequencyHz = 50m;
                if (cfg?.FrequencyHz.HasValue == true && cfg.FrequencyHz.Value > 0)
                    frequencyHz = cfg.FrequencyHz.Value;

                decimal timeMul = 0.001m;
                if (cfg?.TimeMul.HasValue == true && cfg.TimeMul.Value > 0)
                    timeMul = cfg.TimeMul.Value;

                var waveStartTimeUtc = analysis.StartTime ?? analysis.CrtTime;

                var voltageChannels = await ResolveVoltageChannelsAsync(analysis.Id);

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
                    .ToListAsync();

                decimal sagThreshold = detail.SagThresholdPct ?? 90m;
                decimal interruptThreshold = detail.InterruptThresholdPct ?? 10m;
                decimal hysteresis = detail.HysteresisPct ?? 2m;
                decimal minDuration = 10m;

                var computed = DetectPhaseEventsFromRms(
                    rmsPoints.ToArray(),
                    waveStartTimeUtc,
                    sagThreshold,
                    interruptThreshold,
                    hysteresis,
                    minDuration);

                var markers = BuildMarkers(detail, phases, waveStartTimeUtc, rmsPoints, computed);
                var suggested = SuggestSampleRange(computed, rmsPoints.ToArray());

                var data = new ZwavSagProcessDto
                {
                    Event = detail,
                    Phases = phases,
                    AnalysisId = analysis.Id,
                    AnalysisGuid = analysis.AnalysisGuid,
                    WaveStartTimeUtc = waveStartTimeUtc,
                    FrequencyHz = frequencyHz,
                    TimeMul = timeMul,
                    VoltageChannels = voltageChannels,
                    RmsPoints = rmsPoints.ToArray(),
                    Markers = markers,
                    ComputedEvents = computed,
                    SuggestedFromSample = suggested.fromSample,
                    SuggestedToSample = suggested.toSample
                };

                return Ok(new ApiResponse<ZwavSagProcessDto>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("查询暂降分析过程失败", ex);
            }
        }

        [HttpPost("{id:int}/process/preview")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagProcessPreviewResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PreviewProcessAsync(
            [FromRoute] int id,
            [FromBody] ZwavSagProcessPreviewRequest req)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var detail = await _svc.GetDetailAsync(id);
                if (detail == null)
                    return NotFound(new ApiErrorResponse { Message = "事件不存在" });

                var phases = await _svc.GetPhasesAsync(id);

                var analysis = await _context.ZwavAnalyses
                    .AsNoTracking()
                    .Where(x => x.FileId == detail.FileId)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                if (analysis == null)
                    return NotFound(new ApiErrorResponse { Message = "未找到对应解析任务" });

                var waveStartTimeUtc = analysis.StartTime ?? analysis.CrtTime;

                decimal sagThreshold = req?.SagThresholdPct ?? detail.SagThresholdPct ?? 90m;
                decimal interruptThreshold = req?.InterruptThresholdPct ?? detail.InterruptThresholdPct ?? 10m;
                decimal hysteresis = req?.HysteresisPct ?? detail.HysteresisPct ?? 2m;
                decimal minDuration = req?.MinDurationMs ?? 10m;

                decimal? overrideRef = req?.ReferenceVoltage;
                if (overrideRef.HasValue && overrideRef.Value <= 0)
                    overrideRef = null;

                var raw = await _context.ZwavSagRmsPoints
                    .AsNoTracking()
                    .Where(x => x.SagEventId == id)
                    .OrderBy(x => x.SampleNo)
                    .ThenBy(x => x.ChannelIndex)
                    .ThenBy(x => x.SeqNo)
                    .ToListAsync();

                var rmsPoints = raw
                    .Select(x =>
                    {
                        var refV = overrideRef ?? x.ReferenceVoltage;
                        var pct = refV > 0 ? decimal.Round(x.Rms / refV * 100m, 3) : x.RmsPct;

                        return new ZwavSagRmsPointDto
                        {
                            ChannelIndex = x.ChannelIndex,
                            Phase = x.Phase,
                            SampleNo = x.SampleNo,
                            TimeMs = x.TimeMs,
                            Rms = x.Rms,
                            RmsPct = pct,
                            ReferenceVoltage = refV,
                            SeqNo = x.SeqNo
                        };
                    })
                    .ToArray();

                var computed = DetectPhaseEventsFromRms(
                    rmsPoints,
                    waveStartTimeUtc,
                    sagThreshold,
                    interruptThreshold,
                    hysteresis,
                    minDuration);
                var markers = BuildMarkers(detail, phases, waveStartTimeUtc, rmsPoints.ToList(), computed);
                var suggested = SuggestSampleRange(computed, rmsPoints);

                var data = new ZwavSagProcessPreviewResponse
                {
                    RmsPoints = rmsPoints,
                    Markers = markers,
                    ComputedEvents = computed,
                    SuggestedFromSample = suggested.fromSample,
                    SuggestedToSample = suggested.toSample
                };

                return Ok(new ApiResponse<ZwavSagProcessPreviewResponse>
                {
                    Success = true,
                    Message = "预览成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("暂降分析过程预览失败", ex);
            }
        }

        /// <summary>
        /// 分页查询暂降通道词库
        /// </summary>
        [HttpGet("channel-rules")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ZwavSagChannelRuleDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetChannelRuleListAsync(
            [FromQuery] string keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page <= 0)
                    return BadRequestApi("page 必须大于 0");

                if (pageSize <= 0 || pageSize > 200)
                    return BadRequestApi("pageSize 必须在 1~200 之间");

                var query = _context.ZwavSagChannelRules.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(x => x.RuleName.Contains(keyword));
                }

                var total = await query.CountAsync();

                var items = await query
                    .OrderBy(x => x.SeqNo)
                    .ThenBy(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new ZwavSagChannelRuleDto
                    {
                        Id = x.Id,
                        RuleName = x.RuleName,
                        SeqNo = x.SeqNo,
                        CrtTime = x.CrtTime,
                        UpdTime = x.UpdTime
                    })
                    .ToListAsync();

                var data = new PagedResult<ZwavSagChannelRuleDto>
                {
                    Data = items,
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                };

                return Ok(new ApiResponse<PagedResult<ZwavSagChannelRuleDto>>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("查询暂降通道词库失败", ex);
            }
        }

        /// <summary>
        /// 获取暂降通道词库详情
        /// </summary>
        [HttpGet("channel-rules/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagChannelRuleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetChannelRuleDetailAsync([FromRoute] int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var entity = await _context.ZwavSagChannelRules
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (entity == null)
                    return NotFound(new ApiErrorResponse { Message = "词库规则不存在" });

                var data = new ZwavSagChannelRuleDto
                {
                    Id = entity.Id,
                    RuleName = entity.RuleName,
                    SeqNo = entity.SeqNo,
                    CrtTime = entity.CrtTime,
                    UpdTime = entity.UpdTime
                };

                return Ok(new ApiResponse<ZwavSagChannelRuleDto>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("查询暂降通道词库详情失败", ex);
            }
        }

        /// <summary>
        /// 新增暂降通道词库
        /// </summary>
        [HttpPost("channel-rules")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagChannelRuleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateChannelRuleAsync([FromBody] CreateZwavSagChannelRuleRequest req)
        {
            try
            {
                if (req == null)
                    return BadRequestApi("请求体不能为空");

                if (string.IsNullOrWhiteSpace(req.RuleName))
                    return BadRequestApi("RuleName 不能为空");

                var ruleName = req.RuleName.Trim();

                var exists = await _context.ZwavSagChannelRules
                    .AnyAsync(x => x.RuleName == ruleName);

                if (exists)
                    return BadRequestApi("词库规则已存在");

                var now = DateTime.UtcNow;

                var entity = new ZwavSagChannelRule
                {
                    RuleName = ruleName,
                    SeqNo = req.SeqNo,
                    CrtTime = now,
                    UpdTime = now
                };

                _context.ZwavSagChannelRules.Add(entity);
                await _context.SaveChangesAsync();

                var data = new ZwavSagChannelRuleDto
                {
                    Id = entity.Id,
                    RuleName = entity.RuleName,
                    SeqNo = entity.SeqNo,
                    CrtTime = entity.CrtTime,
                    UpdTime = entity.UpdTime
                };

                return Ok(new ApiResponse<ZwavSagChannelRuleDto>
                {
                    Success = true,
                    Message = "新增成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("新增暂降通道词库失败", ex);
            }
        }

        /// <summary>
        /// 修改暂降通道词库
        /// </summary>
        [HttpPut("channel-rules/{id:int}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateChannelRuleAsync(
            [FromRoute] int id,
            [FromBody] UpdateZwavSagChannelRuleRequest req)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                if (req == null)
                    return BadRequestApi("请求体不能为空");

                var entity = await _context.ZwavSagChannelRules
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (entity == null)
                    return NotFound(new ApiErrorResponse { Message = "词库规则不存在" });

                if (!string.IsNullOrWhiteSpace(req.RuleName))
                {
                    var ruleName = req.RuleName.Trim();

                    var exists = await _context.ZwavSagChannelRules
                        .AnyAsync(x => x.Id != id && x.RuleName == ruleName);

                    if (exists)
                        return BadRequestApi("词库规则已存在");

                    entity.RuleName = ruleName;
                }

                if (req.SeqNo.HasValue)
                {
                    entity.SeqNo = req.SeqNo.Value;
                }

                entity.UpdTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "更新成功",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return ServerError("更新暂降通道词库失败", ex);
            }
        }

        /// <summary>
        /// 删除暂降通道词库
        /// </summary>
        [HttpDelete("channel-rules/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteChannelRuleAsync([FromRoute] int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequestApi("id 必须大于 0");

                var entity = await _context.ZwavSagChannelRules
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (entity == null)
                    return NotFound(new ApiErrorResponse { Message = "词库规则不存在" });

                _context.ZwavSagChannelRules.Remove(entity);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return ServerError("删除暂降通道词库失败", ex);
            }
        }

        private async Task<IActionResult> ExecuteAnalyzeAsync(
            AnalyzeZwavSagRequest req,
            string successMessage)
        {
            try
            {
                if (req == null)
                    return BadRequestApi("请求体不能为空");

                if ((req.FileIds == null || req.FileIds.Length == 0) &&
                    (req.AnalysisGuids == null || req.AnalysisGuids.Length == 0))
                    return BadRequestApi("请至少选择一个录波文件");

                var r = await _svc.AnalyzeAsync(req);

                return Ok(new ApiResponse<AnalyzeZwavSagResponse>
                {
                    Success = true,
                    Message = successMessage,
                    Data = r
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerError("暂降分析失败", ex);
            }
        }

        private BadRequestObjectResult BadRequestApi(string message)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = message
            });
        }

        private ObjectResult ServerError(string message, Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiErrorResponse
                {
                    Message = message,
                    Details = ex.Message
                });
        }

        private async Task<ZwavSagVoltageChannelDto[]> ResolveVoltageChannelsAsync(int analysisId)
        {
            var channels = await _context.ZwavChannels
                .AsNoTracking()
                .Where(x => x.AnalysisId == analysisId)
                .Where(x => x.IsEnable == 1)
                .Where(x => x.ChannelType == "Analog")
                .OrderBy(x => x.ChannelIndex)
                .ToListAsync();

            var result = channels
                .Select(ch =>
                {
                    var name = (ch.ChannelName ?? string.Empty).Trim();
                    var code = (ch.ChannelCode ?? string.Empty).Trim();
                    var unit = (ch.Unit ?? string.Empty).Trim();

                    if (!IsVoltageChannel(name, code, unit))
                        return null;

                    var phase = ResolvePhase(name, code);
                    if (string.IsNullOrWhiteSpace(phase))
                        return null;

                    return new ZwavSagVoltageChannelDto
                    {
                        ChannelIndex = ch.ChannelIndex,
                        Phase = phase,
                        ChannelCode = code,
                        ChannelName = name,
                        Unit = unit
                    };
                })
                .Where(x => x != null)
                .ToArray();

            return result;
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

        private static ZwavSagMarkerDto[] BuildMarkers(
            ZwavSagDetailDto detail,
            ZwavSagPhaseDto[] phases,
            DateTime waveStartTimeUtc,
            System.Collections.Generic.List<ZwavSagRmsPointDto> rmsPoints,
            ZwavSagComputedEventDto[] computedEvents = null)
        {
            var list = new System.Collections.Generic.List<ZwavSagMarkerDto>();

            if (computedEvents != null && computedEvents.Length > 0)
            {
                foreach (var evt in computedEvents)
                {
                    list.Add(new ZwavSagMarkerDto { Kind = "EventStart", Phase = evt.Phase, TimeMs = evt.StartMs, Label = $"{evt.Phase} 开始" });
                    list.Add(new ZwavSagMarkerDto { Kind = "EventEnd", Phase = evt.Phase, TimeMs = evt.EndMs, Label = $"{evt.Phase} 结束" });
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
                System.Collections.Generic.IEnumerable<ZwavSagRmsPointDto> scope = rmsPoints.Where(x => x != null);
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
            if (computedEvents == null || computedEvents.Length == 0)
                return (null, null);

            if (rmsPoints == null || rmsPoints.Length == 0)
                return (null, null);

            var startMs = computedEvents.Min(x => x.StartMs);
            var endMs = computedEvents.Max(x => x.EndMs);
            var fromMs = startMs - 1000;
            var toMs = endMs + 1000;

            var hits = rmsPoints
                .Where(x => x != null)
                .Where(x => x.TimeMs >= fromMs && x.TimeMs <= toMs)
                .ToList();

            if (hits.Count == 0)
                return (null, null);

            return (hits.Min(x => x.SampleNo), hits.Max(x => x.SampleNo));
        }

        private static ZwavSagComputedEventDto[] DetectPhaseEventsFromRms(
            ZwavSagRmsPointDto[] rmsPoints,
            DateTime waveStartTimeUtc,
            decimal sagThresholdPct,
            decimal interruptThresholdPct,
            decimal hysteresisPct,
            decimal minDurationMs)
        {
            if (rmsPoints == null || rmsPoints.Length == 0)
                return Array.Empty<ZwavSagComputedEventDto>();

            var phases = rmsPoints
                .Where(x => x != null)
                .Select(x => (x.Phase ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (phases.Count == 0)
                return Array.Empty<ZwavSagComputedEventDto>();

            decimal recoverThreshold = sagThresholdPct + hysteresisPct;
            var results = new System.Collections.Generic.List<ZwavSagComputedEventDto>();

            foreach (var phase in phases)
            {
                var rows = rmsPoints
                    .Where(x => x != null)
                    .Where(x => string.Equals((x.Phase ?? string.Empty).Trim(), phase, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => x.SampleNo)
                    .Select(g => new PhaseRmsRow
                    {
                        SampleNo = g.Key,
                        TimeMs = g.Min(x => x.TimeMs),
                        RmsPct = g.Min(x => x.RmsPct)
                    })
                    .OrderBy(x => x.TimeMs)
                    .ToList();

                if (rows.Count == 0)
                    continue;

                bool inEvent = false;
                int startIndex = -1;
                decimal worstPct = decimal.MaxValue;

                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    bool below = r.RmsPct <= sagThresholdPct;
                    bool recovered = r.RmsPct >= recoverThreshold;

                    if (!inEvent && below)
                    {
                        inEvent = true;
                        startIndex = i;
                        worstPct = r.RmsPct;
                        continue;
                    }

                    if (inEvent)
                    {
                        if (r.RmsPct < worstPct)
                            worstPct = r.RmsPct;

                        if (recovered)
                        {
                            int endIndex = System.Math.Max(startIndex, i - 1);
                            var evt = BuildPhaseEvent(rows, startIndex, endIndex, phase, waveStartTimeUtc, worstPct, interruptThresholdPct);
                            if (evt != null && evt.DurationMs >= minDurationMs)
                                results.Add(evt);

                            inEvent = false;
                            startIndex = -1;
                        }
                    }
                }

                if (inEvent && startIndex >= 0)
                {
                    var evt = BuildPhaseEvent(rows, startIndex, rows.Count - 1, phase, waveStartTimeUtc, worstPct, interruptThresholdPct);
                    if (evt != null && evt.DurationMs >= minDurationMs)
                        results.Add(evt);
                }
            }

            return results
                .OrderBy(x => x.StartMs)
                .ThenBy(x => x.Phase)
                .ToArray();
        }

        private static ZwavSagComputedEventDto BuildPhaseEvent(
            System.Collections.Generic.List<PhaseRmsRow> rows,
            int startIndex,
            int endIndex,
            string phase,
            DateTime waveStartTimeUtc,
            decimal worstPct,
            decimal interruptThresholdPct)
        {
            if (startIndex < 0 || endIndex < startIndex || endIndex >= rows.Count)
                return null;

            var start = rows[startIndex];
            var end = rows[endIndex];

            double startMs = start.TimeMs;
            double endMs = end.TimeMs;
            decimal duration = decimal.Round((decimal)System.Math.Max(0, endMs - startMs), 3);

            var startTimeUtc = waveStartTimeUtc.AddMilliseconds(startMs);
            var endTimeUtc = waveStartTimeUtc.AddMilliseconds(endMs);
            var eventType = worstPct <= interruptThresholdPct ? "Interruption" : "Sag";
            var sagMagnitude = decimal.Round(100m - worstPct, 3);

            return new ZwavSagComputedEventDto
            {
                EventType = eventType,
                Phase = phase,
                OccurTimeUtc = startTimeUtc,
                StartTimeUtc = startTimeUtc,
                EndTimeUtc = endTimeUtc,
                StartMs = startMs,
                EndMs = endMs,
                DurationMs = duration,
                ResidualVoltagePct = decimal.Round(worstPct, 3),
                SagMagnitudePct = sagMagnitude
            };
        }

        private class PhaseRmsRow
        {
            public int SampleNo { get; set; }
            public double TimeMs { get; set; }
            public decimal RmsPct { get; set; }
        }
    }
}
