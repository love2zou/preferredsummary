using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 系统资源控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SystemResourceController : ControllerBase
    {
        private readonly ISystemResourceService _systemResourceService;
        
        public SystemResourceController(ISystemResourceService systemResourceService)
        {
            _systemResourceService = systemResourceService;
        }
        
        /// <summary>
        /// 获取最新的系统资源信息
        /// </summary>
        /// <returns>最新系统资源信息</returns>
        [HttpGet("latest")]
        [ProducesResponseType(typeof(ApiResponse<SystemResourceListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLatestSystemResource()
        {
            try
            {
                var result = await _systemResourceService.GetLatestSystemResource();
                return Ok(new ApiResponse<SystemResourceListDto>
                {
                    Success = true,
                    Message = "获取最新系统资源信息成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取系统资源信息失败", Details = ex.Message });
            }
        }
        
        /// <summary>
        /// 获取一天内的系统资源数据
        /// </summary>
        /// <returns>系统资源列表</returns>
        [HttpGet("daily")]
        [ProducesResponseType(typeof(ApiResponse<SystemResourceListDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDailySystemResourceData()
        {
            try
            {
                var result = await _systemResourceService.GetDailySystemResourceData();
                return Ok(new ApiResponse<SystemResourceListDto[]>
                {
                    Success = true,
                    Message = "获取每日系统资源数据成功",
                    Data = result.ToArray()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取每日系统资源数据失败", Details = ex.Message });
            }
        }
        
        /// <summary>
        /// 获取系统资源统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiResponse<SystemResourceStatsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSystemResourceStats()
        {
            try
            {
                var result = await _systemResourceService.GetSystemResourceStats();
                return Ok(new ApiResponse<SystemResourceStatsDto>
                {
                    Success = true,
                    Message = "获取系统资源统计信息成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取系统资源统计信息失败", Details = ex.Message });
            }
        }
        
        /// <summary>
        /// 创建系统资源记录
        /// </summary>
        /// <param name="resourceDto">系统资源信息</param>
        /// <returns>创建结果</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<SystemResourceListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSystemResource([FromBody] SystemResourceCreateDto resourceDto)
        {
            try
            {
                if (resourceDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }
                
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }
                
                var result = await _systemResourceService.CreateSystemResource(resourceDto);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建系统资源记录失败", Details = ex.Message });
            }
        }
    }
}