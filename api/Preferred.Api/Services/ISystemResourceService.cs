using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 系统资源服务接口
    /// </summary>
    public interface ISystemResourceService
    {
        /// <summary>
        /// 获取最新的系统资源信息
        /// </summary>
        /// <returns>最新系统资源信息</returns>
        Task<SystemResourceListDto> GetLatestSystemResource();
        
        /// <summary>
        /// 获取指定时间范围内的系统资源列表
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>系统资源列表</returns>
        Task<List<SystemResourceListDto>> GetSystemResourceList(DateTime startTime, DateTime endTime);
        
        /// <summary>
        /// 获取一天内的系统资源数据（5分钟间隔）
        /// </summary>
        /// <returns>系统资源列表</returns>
        Task<List<SystemResourceListDto>> GetDailySystemResourceData();
        
        /// <summary>
        /// 创建系统资源记录
        /// </summary>
        /// <param name="resourceDto">系统资源信息</param>
        /// <returns>创建结果</returns>
        Task<ApiResponse<SystemResourceListDto>> CreateSystemResource(SystemResourceCreateDto resourceDto);
        
        /// <summary>
        /// 获取系统资源统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        Task<SystemResourceStatsDto> GetSystemResourceStats();
    }
}