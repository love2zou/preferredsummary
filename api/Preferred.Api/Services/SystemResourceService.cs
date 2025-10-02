using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 系统资源服务实现
    /// </summary>
    public class SystemResourceService : ISystemResourceService
    {
        private readonly ApplicationDbContext _context;
        
        public SystemResourceService(ApplicationDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// 获取最新的系统资源信息
        /// </summary>
        public async Task<SystemResourceListDto> GetLatestSystemResource()
        {
            var latest = await _context.SystemResources
                .OrderByDescending(x => x.CrtTime)
                .FirstOrDefaultAsync();
                
            if (latest == null)
            {
                return new SystemResourceListDto
                {
                    CpuUsage = 0,
                    MemoryUsage = 0,
                    DiskUsage = 0,
                    DiskTotal = 0,
                    DiskUsed = 0,
                    DiskFree = 0,
                    CrtTime = DateTime.Now
                };
            }
            
            return new SystemResourceListDto
            {
                Id = latest.Id,
                CpuUsage = latest.CpuUsage,
                MemoryUsage = latest.MemoryUsage,
                DiskUsage = latest.DiskUsage,
                DiskTotal = latest.DiskTotal,
                DiskUsed = latest.DiskUsed,
                DiskFree = latest.DiskFree,
                CrtTime = latest.CrtTime,
                UpdTime = latest.UpdTime
            };
        }
        
        /// <summary>
        /// 获取指定时间范围内的系统资源列表
        /// </summary>
        public async Task<List<SystemResourceListDto>> GetSystemResourceList(DateTime startTime, DateTime endTime)
        {
            var resources = await _context.SystemResources
                .Where(x => x.CrtTime >= startTime && x.CrtTime <= endTime)
                .OrderBy(x => x.CrtTime)
                .Select(x => new SystemResourceListDto
                {
                    Id = x.Id,
                    CpuUsage = x.CpuUsage,
                    MemoryUsage = x.MemoryUsage,
                    DiskUsage = x.DiskUsage,
                    DiskTotal = x.DiskTotal,
                    DiskUsed = x.DiskUsed,
                    DiskFree = x.DiskFree,
                    CrtTime = x.CrtTime,
                    UpdTime = x.UpdTime
                })
                .ToListAsync();
                
            return resources;
        }
        
        /// <summary>
        /// 获取一天内的系统资源数据（5分钟间隔）
        /// </summary>
        public async Task<List<SystemResourceListDto>> GetDailySystemResourceData()
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddDays(-1);
            
            // 获取过去24小时的数据，按5分钟间隔分组
            var resources = await _context.SystemResources
                .Where(x => x.CrtTime >= startTime && x.CrtTime <= endTime)
                .OrderBy(x => x.CrtTime)
                .ToListAsync();
                
            // 按5分钟间隔分组并取平均值
            var groupedData = resources
                .GroupBy(x => new DateTime(
                    x.CrtTime.Year,
                    x.CrtTime.Month,
                    x.CrtTime.Day,
                    x.CrtTime.Hour,
                    (x.CrtTime.Minute / 5) * 5,
                    0))
                .Select(g => new SystemResourceListDto
                {
                    CpuUsage = Math.Round(g.Average(x => x.CpuUsage), 2),
                    MemoryUsage = Math.Round(g.Average(x => x.MemoryUsage), 2),
                    DiskUsage = Math.Round(g.Average(x => x.DiskUsage), 2),
                    DiskTotal = g.First().DiskTotal,
                    DiskUsed = (long)g.Average(x => x.DiskUsed),
                    DiskFree = (long)g.Average(x => x.DiskFree),
                    CrtTime = g.Key
                })
                .OrderBy(x => x.CrtTime)
                .ToList();
                
            return groupedData;
        }
        
        /// <summary>
        /// 创建系统资源记录
        /// </summary>
        public async Task<ApiResponse<SystemResourceListDto>> CreateSystemResource(SystemResourceCreateDto resourceDto)
        {
            try
            {
                // 在CreateSystemResource方法中
                var resource = new SystemResource
                {
                    HostName = resourceDto.HostName,
                    CpuUsage = resourceDto.CpuUsage,
                    MemoryUsage = resourceDto.MemoryUsage,
                    DiskName = resourceDto.DiskName,
                    DiskUsage = resourceDto.DiskUsage,
                    DiskTotal = resourceDto.DiskTotal,
                    DiskUsed = resourceDto.DiskUsed,
                    DiskFree = resourceDto.DiskFree,
                    CrtTime = DateTime.Now,
                    UpdTime = DateTime.Now
                };
                
                _context.SystemResources.Add(resource);
                await _context.SaveChangesAsync();
                
                var result = new SystemResourceListDto
                {
                    Id = resource.Id,
                    HostName = resource.HostName,
                    CpuUsage = resource.CpuUsage,
                    MemoryUsage = resource.MemoryUsage,
                    DiskName = resource.DiskName,
                    DiskUsage = resource.DiskUsage,
                    DiskTotal = resource.DiskTotal,
                    DiskUsed = resource.DiskUsed,
                    DiskFree = resource.DiskFree,
                    CrtTime = resource.CrtTime,
                    UpdTime = resource.UpdTime
                };
                
                return new ApiResponse<SystemResourceListDto>
                {
                    Success = true,
                    Message = "创建系统资源记录成功",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<SystemResourceListDto>
                {
                    Success = false,
                    Message = $"创建系统资源记录失败: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// 获取系统资源统计信息
        /// </summary>
        public async Task<SystemResourceStatsDto> GetSystemResourceStats()
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddDays(-1);
            
            var resources = await _context.SystemResources
                .Where(x => x.CrtTime >= startTime)
                .ToListAsync();
                
            if (!resources.Any())
            {
                return new SystemResourceStatsDto();
            }
            
            return new SystemResourceStatsDto
            {
                AvgCpuUsage = Math.Round(resources.Average(x => x.CpuUsage), 2),
                MaxCpuUsage = Math.Round(resources.Max(x => x.CpuUsage), 2),
                AvgMemoryUsage = Math.Round(resources.Average(x => x.MemoryUsage), 2),
                MaxMemoryUsage = Math.Round(resources.Max(x => x.MemoryUsage), 2),
                AvgDiskUsage = Math.Round(resources.Average(x => x.DiskUsage), 2),
                MaxDiskUsage = Math.Round(resources.Max(x => x.DiskUsage), 2)
            };
        }
    }
}