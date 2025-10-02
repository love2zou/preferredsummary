using System;
using System.ComponentModel.DataAnnotations;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 系统资源创建DTO
    /// </summary>
    public class SystemResourceCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string HostName { get; set; } = string.Empty;

        [Required]
        [Range(0, 100)]
        public decimal CpuUsage { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal MemoryUsage { get; set; }

        [Required]
        [MaxLength(100)]
        public string DiskName { get; set; } = string.Empty;

        [Required]
        [Range(0, 100)]
        public decimal DiskUsage { get; set; }

        /// <summary>
        /// 总磁盘容量，单位GB
        /// </summary>
        [Required]
        public decimal DiskTotal { get; set; }

        /// <summary>
        /// 已使用磁盘容量，单位GB
        /// </summary>
        [Required]
        public decimal DiskUsed { get; set; }

        /// <summary>
        /// 可用磁盘容量，单位GB
        /// </summary>
        [Required]
        public decimal DiskFree { get; set; }
    }

    /// <summary>
    /// 系统资源列表DTO
    /// </summary>
    public class SystemResourceListDto
    {
        public int Id { get; set; }
        public string HostName { get; set; } = string.Empty;
        public decimal CpuUsage { get; set; }
        public decimal MemoryUsage { get; set; }
        public string DiskName { get; set; } = string.Empty;
        public decimal DiskUsage { get; set; }
        public decimal DiskTotal { get; set; }
        public decimal DiskUsed { get; set; }
        public decimal DiskFree { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 系统资源搜索参数
    /// </summary>
    public class SystemResourceSearchParams
    {
        public int? Page { get; set; } = 1;
        public int? PageSize { get; set; } = 10;
        public string? HostName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? MinCpuUsage { get; set; }
        public decimal? MaxCpuUsage { get; set; }
        public decimal? MinMemoryUsage { get; set; }
        public decimal? MaxMemoryUsage { get; set; }
    }

    /// <summary>
    /// 系统资源统计DTO
    /// </summary>
    public class SystemResourceStatsDto
    {
        public decimal AvgCpuUsage { get; set; }
        public decimal MaxCpuUsage { get; set; }
        public decimal AvgMemoryUsage { get; set; }
        public decimal MaxMemoryUsage { get; set; }
        public decimal AvgDiskUsage { get; set; }
        public decimal MaxDiskUsage { get; set; }
    }
}