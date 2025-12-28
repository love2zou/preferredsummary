using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 数据解析控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class DataParserController : ControllerBase
    {
        private readonly IDataParserService _dataParserService;
        private readonly ILogger<DataParserController> _logger;

        public DataParserController(IDataParserService dataParserService, ILogger<DataParserController> logger)
        {
            _dataParserService = dataParserService;
            _logger = logger;
        }

        /// <summary>
        /// 上传并解析 DAT 文件
        /// </summary>
        /// <param name="file">DAT 文件</param>
        /// <returns>解析任务ID</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请选择要上传的文件" });
                }

                if (!file.FileName.ToLower().EndsWith(".dat"))
                {
                    return BadRequest(new ApiErrorResponse { Message = "只支持 .dat 格式的文件" });
                }

                if (file.Length > 10 * 1024 * 1024) // 10MB
                {
                    return BadRequest(new ApiErrorResponse { Message = "文件大小不能超过 10MB" });
                }

                var taskId = await _dataParserService.StartParseAsync(file);
                
                _logger.LogInformation($"开始解析文件: {file.FileName}, 任务ID: {taskId}");
                
                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = "文件上传成功，开始解析",
                    Data = taskId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件上传失败");
                return StatusCode(500, new ApiErrorResponse { Message = "文件上传失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 获取解析状态和结果
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>解析状态和结果</returns>
        [HttpGet("status/{taskId}")]
        [ProducesResponseType(typeof(ApiResponse<DataParseResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetParseStatus(string taskId)
        {
            try
            {
                var response = await _dataParserService.GetParseStatusAsync(taskId);
                
                if (response.Status == ParseStatus.Failed && response.Logs.Count == 0)
                {
                    return NotFound(new ApiErrorResponse { Message = "解析任务不存在" });
                }

                return Ok(new ApiResponse<DataParseResponse>
                {
                    Success = true,
                    Message = "获取解析状态成功",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取解析状态失败, 任务ID: {taskId}");
                return StatusCode(500, new ApiErrorResponse { Message = "获取解析状态失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 导出解析结果为 Excel 文件
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="fileName">文件名（可选）</param>
        /// <returns>Excel 文件</returns>
        [HttpGet("export/{taskId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ExportToExcel(string taskId, [FromQuery] string fileName = null)
        {
            try
            {
                var (stream, exportFileName, contentType) = await _dataParserService.ExportToExcelAsync(taskId, fileName);
                
                _logger.LogInformation($"导出 Excel 文件: {exportFileName}, 任务ID: {taskId}");
                
                return File(stream, contentType, exportFileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"导出 Excel 失败, 任务ID: {taskId}");
                return StatusCode(500, new ApiErrorResponse { Message = "导出 Excel 失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 清理过期的解析任务
        /// </summary>
        /// <returns>清理结果</returns>
        [HttpPost("cleanup")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CleanupExpiredTasks()
        {
            try
            {
                await _dataParserService.CleanupExpiredTasksAsync();
                
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "清理过期任务完成"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期任务失败");
                return StatusCode(500, new ApiErrorResponse { Message = "清理过期任务失败", Details = ex.Message });
            }
        }
    }
}