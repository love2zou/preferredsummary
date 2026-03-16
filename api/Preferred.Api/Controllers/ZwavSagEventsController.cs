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
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ZwavSagEventsController : ControllerBase
    {
        private readonly IZwavSagEventService _svc;
        private readonly ApplicationDbContext _context;

        public ZwavSagEventsController(IZwavSagEventService svc, ApplicationDbContext context)
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
        /// 根据录波解析任务ID查询暂降结果
        /// </summary>
        [HttpGet("by-analysis/{analysisId:int}")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagDetailDto[]>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetByAnalysisAsync([FromRoute] int analysisId)
        {
            try
            {
                if (analysisId <= 0)
                    return BadRequestApi("analysisId 必须大于 0");

                var data = await _svc.GetByAnalysisIdAsync(analysisId);

                return Ok(new ApiResponse<ZwavSagDetailDto[]>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return ServerError("按解析任务查询暂降结果失败", ex);
            }
        }

        /// <summary>
        /// 发起电压暂降分析
        /// 建议由 AnalysisId 驱动，从已解析入库的录波数据中计算暂降事件。
        /// 当前实现默认会重建该 AnalysisId 下的暂降结果。
        /// </summary>
        [HttpPost("analyze")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<AnalyzeZwavSagResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AnalyzeAsync([FromBody] AnalyzeZwavSagRequest req)
        {
            try
            {
                if (req == null)
                    return BadRequestApi("请求体不能为空");

                if (req.AnalysisGuids == null || req.AnalysisGuids.Length == 0)
                    return BadRequestApi("AnalysisGuids 必须包含至少一个分析任务 ID");

                var r = await _svc.AnalyzeAsync(req);

                return Ok(new ApiResponse<AnalyzeZwavSagResponse>
                {
                    Success = true,
                    Message = "分析完成",
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

        /// <summary>
        /// 重新分析指定解析任务的暂降结果
        /// 当前实现与 AnalyzeAsync 一致：都会先清理旧结果，再重新计算。
        /// 保留该接口是为了前端语义更清晰。
        /// </summary>
        [HttpPost("reanalyze")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<AnalyzeZwavSagResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReAnalyzeAsync([FromBody] AnalyzeZwavSagRequest req)
        {
            try
            {
                if (req == null)
                    return BadRequestApi("请求体不能为空");

                if (req.AnalysisGuids == null || req.AnalysisGuids.Length == 0)
                    return BadRequestApi("AnalysisGuids 必须包含至少一个分析任务 ID");

                var r = await _svc.AnalyzeAsync(req);

                return Ok(new ApiResponse<AnalyzeZwavSagResponse>
                {
                    Success = true,
                    Message = "重新分析完成",
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
                return ServerError("重新分析失败", ex);
            }
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
            [FromBody] UpdateZwavSagEventRequest req,
            CancellationToken ct = default)
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
        /// 按解析任务删除全部暂降结果
        /// </summary>
        [HttpDelete("by-analysis/{analysisId:int}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteByAnalysisAsync([FromRoute] int analysisId)
        {
            try
            {
                if (analysisId <= 0)
                    return BadRequestApi("analysisId 必须大于 0");

                var deletedCount = await _svc.DeleteByAnalysisIdAsync(analysisId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"删除成功，共删除 {deletedCount} 条事件",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return ServerError("按解析任务删除暂降结果失败", ex);
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
    }
}