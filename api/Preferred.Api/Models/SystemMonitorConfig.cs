using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 系统监控配置
    /// </summary>
    public class SystemMonitorConfig
    {
        /// <summary>
        /// 监控间隔时间（分钟）
        /// </summary>
        public int IntervalMinutes { get; set; } = 1;
        
        /// <summary>
        /// 是否启用监控
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 主机名
        /// </summary>
        public string HostName { get; set; } = Environment.MachineName;
        
        /// <summary>
        /// 磁盘名称
        /// </summary>
        public string DiskName { get; set; } = "C:";
    }
}