using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        public ZwavSagEventsController(
            IZwavSagEventService svc)
        {
            _svc = svc;
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

        [HttpGet("{id:int}/process")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagProcessDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProcessAsync([FromRoute] int id)
        {
            try
            {
                var data = await _svc.GetProcessAsync(id);

                return Ok(new ApiResponse<ZwavSagProcessDto>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiErrorResponse { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
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
                var data = await _svc.PreviewProcessAsync(id, req);

                return Ok(new ApiResponse<ZwavSagProcessPreviewResponse>
                {
                    Success = true,
                    Message = "预览成功",
                    Data = data
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiErrorResponse { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
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
                var data = await _svc.QueryChannelRuleAsync(keyword, page, pageSize);

                return Ok(new ApiResponse<PagedResult<ZwavSagChannelRuleDto>>
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
                return ServerError("查询暂降通道词库失败", ex);
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
                var data = await _svc.CreateChannelRuleAsync(req);

                return Ok(new ApiResponse<ZwavSagChannelRuleDto>
                {
                    Success = true,
                    Message = "新增成功",
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
                var ok = await _svc.UpdateChannelRuleAsync(id, req);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "词库规则不存在" });

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
                var ok = await _svc.DeleteChannelRuleAsync(id);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "词库规则不存在" });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功",
                    Data = null
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerError("删除暂降通道词库失败", ex);
            }
        }

        [HttpGet("group-rules")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ZwavSagGroupRuleDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetGroupRuleListAsync(
            [FromQuery] string keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var data = await _svc.QueryGroupRuleAsync(keyword, page, pageSize);

                return Ok(new ApiResponse<PagedResult<ZwavSagGroupRuleDto>>
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
                return ServerError("查询暂降分组词库失败", ex);
            }
        }

        [HttpPost("group-rules")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<ZwavSagGroupRuleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateGroupRuleAsync([FromBody] CreateZwavSagGroupRuleRequest req)
        {
            try
            {
                var data = await _svc.CreateGroupRuleAsync(req);

                return Ok(new ApiResponse<ZwavSagGroupRuleDto>
                {
                    Success = true,
                    Message = "新增成功",
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
                return ServerError("新增暂降分组词库失败", ex);
            }
        }

        [HttpPut("group-rules/{id:int}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateGroupRuleAsync(
            [FromRoute] int id,
            [FromBody] UpdateZwavSagGroupRuleRequest req)
        {
            try
            {
                var ok = await _svc.UpdateGroupRuleAsync(id, req);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "分组规则不存在" });

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
                return ServerError("更新暂降分组词库失败", ex);
            }
        }

        [HttpDelete("group-rules/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteGroupRuleAsync([FromRoute] int id)
        {
            try
            {
                var ok = await _svc.DeleteGroupRuleAsync(id);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "分组规则不存在" });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功",
                    Data = null
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestApi(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerError("删除暂降分组词库失败", ex);
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
    }
}
