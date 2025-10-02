using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 定时任务控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ScheduledTaskController : ControllerBase
    {
        private readonly IScheduledTaskService _scheduledTaskService;
        
        public ScheduledTaskController(IScheduledTaskService scheduledTaskService)
        {
            _scheduledTaskService = scheduledTaskService;
        }
        
        /// <summary>
        /// 获取所有定时任务
        /// </summary>
        /// <returns>定时任务列表</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<ScheduledTaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllTasks([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            try
            {
                var tasks = await _scheduledTaskService.GetAllTasksAsync();
                var totalCount = tasks.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / size);
                
                var pagedTasks = tasks.Skip((page - 1) * size).Take(size).ToList();
                
                var response = new PagedResponse<ScheduledTaskDto>
                {
                    Data = pagedTasks,
                    Total = totalCount,
                    Page = page,
                    PageSize = size,
                    TotalPages = totalPages
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取定时任务列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>定时任务信息</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ScheduledTaskDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTaskById(int id)
        {
            try
            {
                var task = await _scheduledTaskService.GetTaskByIdAsync(id);
                if (task == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "定时任务不存在" });
                }
                
                return Ok(task);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建定时任务
        /// </summary>
        /// <param name="taskDto">任务信息</param>
        /// <returns>创建的任务信息</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateTask([FromBody] ScheduledTaskDto taskDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求参数无效" });
                }
                
                var createdTask = await _scheduledTaskService.CreateTaskAsync(taskDto);
                return CreatedAtAction(nameof(GetTaskById), new { id = createdTask.Id }, 
                    new ApiResponse { Success = true, Message = "定时任务创建成功" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 更新定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="taskDto">任务信息</param>
        /// <returns>更新的任务信息</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] ScheduledTaskDto taskDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求参数无效" });
                }
                
                var updatedTask = await _scheduledTaskService.UpdateTaskAsync(id, taskDto);
                if (updatedTask == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "定时任务不存在" });
                }
                
                return Ok(new ApiResponse { Success = true, Message = "定时任务更新成功" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "更新定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                var result = await _scheduledTaskService.DeleteTaskAsync(id);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "定时任务不存在" });
                }
                
                return Ok(new ApiResponse { Success = true, Message = "删除定时任务成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 批量删除定时任务
        /// </summary>
        /// <param name="request">批量删除请求</param>
        /// <returns>删除结果</returns>
        [HttpDelete("batch")]
        [ProducesResponseType(typeof(ApiResponse<BatchDeleteTaskResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BatchDeleteTasks([FromBody] BatchDeleteTaskRequest request)
        {
            try
            {
                if (request?.Ids == null || request.Ids.Length == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请选择要删除的任务" });
                }
                
                var result = await _scheduledTaskService.BatchDeleteTasksAsync(request.Ids);
                return Ok(new ApiResponse<BatchDeleteTaskResult>
                {
                    Success = true,
                    Message = $"批量删除完成，成功：{result.SuccessCount}，失败：{result.FailCount}",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "批量删除定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 启用/禁用定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="request">启用/禁用请求</param>
        /// <returns>操作结果</returns>
        [HttpPatch("{id}/enabled")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SetTaskEnabled(int id, [FromBody] TaskEnabledRequest request)
        {
            try
            {
                var result = await _scheduledTaskService.SetTaskEnabledAsync(id, request.Enabled);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "定时任务不存在" });
                }
                
                return Ok(new ApiResponse { Success = true, Message = $"{(request.Enabled ? "启用" : "禁用")}定时任务成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = $"{(request.Enabled ? "启用" : "禁用")}定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 手动执行定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>执行结果</returns>
        [HttpPost("{id}/execute")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExecuteTask(int id)
        {
            try
            {
                var result = await _scheduledTaskService.ExecuteTaskAsync(id);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "定时任务不存在或执行失败" });
                }
                
                return Ok(new ApiResponse { Success = true, Message = "手动执行定时任务成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "手动执行定时任务失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 获取任务执行日志
        /// </summary>
        /// <param name="taskId">任务ID（可选）</param>
        /// <param name="success">执行结果（可选）</param>
        /// <param name="startTime">开始时间（可选）</param>
        /// <param name="endTime">结束时间（可选）</param>
        /// <param name="page">页码</param>
        /// <param name="size">页大小</param>
        /// <returns>执行日志列表</returns>
        [HttpGet("logs")]
        [ProducesResponseType(typeof(PagedResponse<ScheduledTaskLogDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTaskLogs(
            [FromQuery] int? taskId, 
            [FromQuery] bool? success,
            [FromQuery] DateTime? startTime,
            [FromQuery] DateTime? endTime,
            [FromQuery] int page = 1, 
            [FromQuery] int size = 20)
        {
            try
            {
                var (logs, total) = await _scheduledTaskService.GetTaskLogsAsync(taskId, success, startTime, endTime, page, size);
                var totalPages = (int)Math.Ceiling((double)total / size);
                
                var response = new PagedResponse<ScheduledTaskLogDto>
                {
                    Data = logs,
                    Total = total,
                    Page = page,
                    PageSize = size,
                    TotalPages = totalPages
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取任务执行日志失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 清理过期日志
        /// </summary>
        [HttpDelete("logs/cleanup")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> CleanExpiredLogs([FromQuery] int daysToKeep = 30)
        {
            try
            {
                var result = await _scheduledTaskService.CleanExpiredLogsAsync(daysToKeep);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "日志清理完成",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse
                {
                    Message = "清理日志失败",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// 初始化系统资源监控定时任务
        /// </summary>
        /// <returns>初始化结果</returns>
        [HttpPost("init-system-monitor")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InitSystemMonitorTask()
        {
            try
            {
                // 检查是否已存在系统资源监控任务
                var existingTask = await _scheduledTaskService.GetTaskByCodeAsync("SYSTEM_RESOURCE_MONITOR");
                if (existingTask != null)
                {
                    return Ok(new ApiResponse { Success = true, Message = "系统资源监控任务已存在" });
                }
        
                // 创建系统资源监控定时任务
                var taskDto = new ScheduledTaskDto
                {
                    Name = "系统资源监控",
                    Code = "SYSTEM_RESOURCE_MONITOR",
                    Cron = "0 */5 * * * ?", // 每5分钟执行一次
                    Handler = "Preferred.Api.Services.SystemMonitorService.MonitorAndSaveAsync",
                    Parameters = null,
                    Enabled = true,
                    Remark = "定期监控系统CPU、内存、磁盘使用情况并保存到数据库，每5分钟执行一次"
                };
        
                var createdTask = await _scheduledTaskService.CreateTaskAsync(taskDto);
                return Ok(new ApiResponse { Success = true, Message = "系统资源监控定时任务创建成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建系统资源监控定时任务失败", Details = ex.Message });
            }
        }
    }
}