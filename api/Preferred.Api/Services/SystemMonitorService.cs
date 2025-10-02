#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Preferred.Api.Models;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;
using System.Management; // 添加此引用用于 WMI 查询

#if WINDOWS
using System.Diagnostics;
#endif

namespace Preferred.Api.Services
{
    /// <summary>
    /// 跨平台系统监控服务实现
    /// </summary>
    public class SystemMonitorService : ISystemMonitorService
    {
        private readonly ILogger<SystemMonitorService> _logger;
        private readonly ISystemResourceService _systemResourceService;
        private readonly SystemMonitorConfig _config;
        private readonly bool _isWindows;
        private readonly bool _isLinux;
        
#if WINDOWS
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _memoryCounter;
#endif

        public SystemMonitorService(
            ILogger<SystemMonitorService> logger,
            ISystemResourceService systemResourceService,
            IOptions<SystemMonitorConfig> config)
        {
            _logger = logger;
            _systemResourceService = systemResourceService;
            _config = config.Value;
            
            // 检测操作系统
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            
#if WINDOWS
            // 只在Windows上初始化性能计数器
            if (_isWindows)
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "无法初始化Windows性能计数器，将使用替代方法");
                }
            }
#endif
            
            _logger.LogInformation($"系统监控服务已初始化 - 操作系统: {(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "Other")}");
        }
        
        
        /// <summary>
        /// 获取当前系统资源使用情况
        /// </summary>
        public async Task<SystemResourceCreateDto> GetCurrentSystemResourceAsync()
        {
            try
            {
                // 获取CPU使用率
                var cpuUsage = await GetCpuUsageAsync();
                
                // 获取内存使用率
                var memoryUsage = await GetMemoryUsageAsync();
                
                // 获取适合当前平台的磁盘路径
                var platformDiskPath = GetPlatformSpecificDiskPath(_config.DiskName);
                
                // 获取磁盘使用情况
                var diskInfo = await GetDiskUsageAsync(platformDiskPath);
                
                _logger.LogInformation($"系统资源监控 - 配置磁盘: {_config.DiskName}, 实际监控: {platformDiskPath}, CPU: {cpuUsage:F2}%, 内存: {memoryUsage:F2}%, 磁盘: {diskInfo.UsagePercentage:F2}%, 使用内存: {diskInfo.UsedBytes}");
                
                return new SystemResourceCreateDto
                {
                    HostName = _config.HostName,
                    CpuUsage = (decimal)cpuUsage,
                    MemoryUsage = (decimal)memoryUsage,
                    DiskName = platformDiskPath, // 使用实际监控的磁盘路径
                    DiskUsage = (decimal)diskInfo.UsagePercentage,
                    DiskTotal = Math.Round((decimal)diskInfo.TotalBytes / (1024 * 1024 * 1024), 2),
                    DiskUsed = Math.Round((decimal)diskInfo.UsedBytes / (1024 * 1024 * 1024), 2),
                    DiskFree = Math.Round((decimal)diskInfo.FreeBytes / (1024 * 1024 * 1024), 2)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取系统资源信息时发生错误");
                throw;
            }
        }
        
        /// <summary>
        /// 执行系统资源监控并保存到数据库
        /// </summary>
        public async Task MonitorAndSaveAsync()
        {
            try
            {
                _logger.LogInformation("开始执行系统资源监控...");
                
                var resourceData = await GetCurrentSystemResourceAsync();
                var result = await _systemResourceService.CreateSystemResource(resourceData);
                
                if (result.Success)
                {
                    _logger.LogInformation($"系统资源监控数据保存成功 - CPU: {resourceData.CpuUsage:F2}%, 内存: {resourceData.MemoryUsage:F2}%, 磁盘: {resourceData.DiskUsage:F2}%");
                }
                else
                {
                    _logger.LogError($"系统资源监控数据保存失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行系统资源监控时发生错误");
            }
        }
        
        /// <summary>
        /// 跨平台获取CPU使用率
        /// </summary>
        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                if (_isWindows)
                {
                    return await GetWindowsCpuUsageAsync();
                }
                else if (_isLinux)
                {
                    return await GetLinuxCpuUsageAsync();
                }
                else
                {
                    _logger.LogWarning("不支持的操作系统，无法获取CPU使用率");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法获取CPU使用率，返回默认值0");
                return 0;
            }
        }
        
        /// <summary>
        /// Windows系统CPU使用率
        /// </summary>
        private async Task<double> GetWindowsCpuUsageAsync()
        {
#if WINDOWS
            if (_cpuCounter != null)
            {
                // 使用性能计数器
                _cpuCounter.NextValue();
                await Task.Delay(1000);
                return Math.Round(_cpuCounter.NextValue(), 2);
            }
            else
#endif
            {
                // 使用进程方式作为备选
                return await GetCpuUsageViaProcessAsync();
            }
        }
        
        /// <summary>
        /// Linux系统CPU使用率
        /// </summary>
        private async Task<double> GetLinuxCpuUsageAsync()
        {
            try
            {
                // 读取/proc/stat文件获取CPU使用率
                var stat1 = await ReadProcStatAsync();
                await Task.Delay(1000);
                var stat2 = await ReadProcStatAsync();
                
                if (stat1 != null && stat2 != null)
                {
                    var idle1 = stat1.Idle + stat1.IOWait;
                    var idle2 = stat2.Idle + stat2.IOWait;
                    
                    var total1 = stat1.User + stat1.Nice + stat1.System + stat1.Idle + stat1.IOWait + stat1.IRQ + stat1.SoftIRQ;
                    var total2 = stat2.User + stat2.Nice + stat2.System + stat2.Idle + stat2.IOWait + stat2.IRQ + stat2.SoftIRQ;
                    
                    var totalDiff = total2 - total1;
                    var idleDiff = idle2 - idle1;
                    
                    if (totalDiff > 0)
                    {
                        var cpuUsage = (1.0 - (double)idleDiff / totalDiff) * 100;
                        return Math.Round(Math.Max(0, Math.Min(100, cpuUsage)), 2);
                    }
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取Linux CPU使用率失败");
                return 0;
            }
        }
        
        /// <summary>
        /// 通过进程方式获取CPU使用率（备选方案）
        /// </summary>
        private async Task<double> GetCpuUsageViaProcessAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
                
                await Task.Delay(1000);
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                return Math.Round(cpuUsageTotal * 100, 2);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 跨平台获取内存使用率
        /// </summary>
        private async Task<double> GetMemoryUsageAsync()
        {
            try
            {
                if (_isWindows)
                {
                    return await GetWindowsMemoryUsageAsync();
                }
                else if (_isLinux)
                {
                    return await GetLinuxMemoryUsageAsync();
                }
                else
                {
                    _logger.LogWarning("不支持的操作系统，无法获取内存使用率");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法获取内存使用率，返回默认值0");
                return 0;
            }
        }

        /// <summary>
        /// Windows系统内存使用率
        /// </summary>
        private async Task<double> GetWindowsMemoryUsageAsync()
        {
        #if WINDOWS
            // 方案1：使用性能计数器
            if (_memoryCounter != null)
            {
                try
                {
                    var availableMemoryMB = _memoryCounter.NextValue();
                    
                    // 获取系统总内存 - 使用 GC 信息估算
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    var totalPhysicalMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
                    var availablePhysicalMemory = (long)(availableMemoryMB * 1024 * 1024);
                    
                    if (totalPhysicalMemory > 0)
                    {
                        var usedMemory = totalPhysicalMemory - availablePhysicalMemory;
                        var memoryUsagePercentage = (double)usedMemory / totalPhysicalMemory * 100;
                        
                        _logger.LogDebug($"Windows内存使用率 - 总内存: {totalPhysicalMemory / (1024 * 1024 * 1024):F2}GB, 已用: {usedMemory / (1024 * 1024 * 1024):F2}GB, 使用率: {memoryUsagePercentage:F2}%");
                        
                        // 确保返回正确的值
                        var result = Math.Round(Math.Max(0, Math.Min(100, memoryUsagePercentage)), 2);
                        if (result > 0)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "性能计数器获取内存使用率失败，尝试备用方案");
                }
            }
        #endif
            
            // 备用方案：使用进程方式
            return await GetWindowsMemoryUsageViaProcessAsync();
        }

#if WINDOWS
        /// <summary>
        /// 通过 WMI 获取 Windows 内存使用率
        /// </summary>
        private async Task<double> GetWindowsMemoryUsageViaWMIAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var totalMemory = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                            
                            using (var memSearcher = new ManagementObjectSearcher("SELECT AvailableBytes FROM Win32_PerfRawData_PerfOS_Memory"))
                            {
                                foreach (ManagementObject memObj in memSearcher.Get())
                                {
                                    var availableMemory = Convert.ToUInt64(memObj["AvailableBytes"]);
                                    var usedMemory = totalMemory - availableMemory;
                                    var memoryUsagePercentage = (double)usedMemory / totalMemory * 100;
                                    
                                    _logger.LogDebug($"WMI内存使用率 - 总内存: {totalMemory / (1024 * 1024 * 1024):F2}GB, 已用: {usedMemory / (1024 * 1024 * 1024):F2}GB, 使用率: {memoryUsagePercentage:F2}%");
                                    
                                    return Math.Round(memoryUsagePercentage, 2);
                                }
                            }
                        }
                    }
                });
                
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WMI查询内存信息失败");
                throw;
            }
        }
#endif

        /// <summary>
        /// 通过进程信息估算 Windows 内存使用率
        /// </summary>
        private async Task<double> GetWindowsMemoryUsageViaProcessAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    // 获取所有进程的内存使用情况
                    var processes = Process.GetProcesses();
                    long totalWorkingSet = 0;
                    
                    foreach (var process in processes)
                    {
                        try
                        {
                            totalWorkingSet += process.WorkingSet64;
                        }
                        catch
                        {
                            // 某些系统进程可能无法访问，忽略错误
                        }
                        finally
                        {
                            process?.Dispose();
                        }
                    }
                    
                    // 估算系统总内存（这是一个粗略的估算）
                    var estimatedTotalMemory = Math.Max(totalWorkingSet * 1.5, 4L * 1024 * 1024 * 1024); // 至少假设4GB
                    var memoryUsagePercentage = (double)totalWorkingSet / estimatedTotalMemory * 100;
                    
                    _logger.LogDebug($"进程估算内存使用率 - 进程总内存: {totalWorkingSet / (1024 * 1024 * 1024):F2}GB, 估算总内存: {estimatedTotalMemory / (1024 * 1024 * 1024):F2}GB, 使用率: {memoryUsagePercentage:F2}%");
                    
                    return Math.Round(Math.Min(100, memoryUsagePercentage), 2);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "进程内存估算失败");
                return 0;
            }
        }

        /// <summary>
        /// Linux 系统内存使用率
        /// </summary>
        private async Task<double> GetLinuxMemoryUsageAsync()
        {
            try
            {
                var memInfo = await ReadProcMeminfoAsync();
                if (memInfo != null && memInfo.MemTotal > 0)
                {
                    // Linux 内存计算：已用内存 = 总内存 - 可用内存 - 缓冲区 - 缓存
                    var usedMemory = memInfo.MemTotal - memInfo.MemFree - memInfo.Buffers - memInfo.Cached;
                    var memoryUsagePercentage = (double)usedMemory / memInfo.MemTotal * 100;
                    
                    _logger.LogDebug($"Linux内存使用率 - 总内存: {memInfo.MemTotal / (1024 * 1024 * 1024):F2}GB, 已用: {usedMemory / (1024 * 1024 * 1024):F2}GB, 使用率: {memoryUsagePercentage:F2}%");
                    
                    return Math.Round(Math.Max(0, Math.Min(100, memoryUsagePercentage)), 2);
                }
                else
                {
                    _logger.LogWarning("无法读取 /proc/meminfo 或内存信息无效");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取Linux内存使用率失败");
                return 0;
            }
        }

        /// <summary>
        /// 读取 /proc/meminfo 文件获取内存信息（优化版本）
        /// </summary>
        private async Task<MemInfo?> ReadProcMeminfoAsync()
        {
            try
            {
                if (!File.Exists("/proc/meminfo"))
                {
                    _logger.LogWarning("/proc/meminfo 文件不存在");
                    return null;
                }
                    
                var lines = await File.ReadAllLinesAsync("/proc/meminfo");
                var memInfo = new MemInfo();
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    if (line.StartsWith("MemTotal:"))
                    {
                        var match = Regex.Match(line, @"(\d+)");
                        if (match.Success)
                            memInfo.MemTotal = long.Parse(match.Value) * 1024; // 转换为字节
                    }
                    else if (line.StartsWith("MemFree:"))
                    {
                        var match = Regex.Match(line, @"(\d+)");
                        if (match.Success)
                            memInfo.MemFree = long.Parse(match.Value) * 1024;
                    }
                    else if (line.StartsWith("Buffers:"))
                    {
                        var match = Regex.Match(line, @"(\d+)");
                        if (match.Success)
                            memInfo.Buffers = long.Parse(match.Value) * 1024;
                    }
                    else if (line.StartsWith("Cached:"))
                    {
                        var match = Regex.Match(line, @"(\d+)");
                        if (match.Success)
                            memInfo.Cached = long.Parse(match.Value) * 1024;
                    }
                    
                    // 如果所有需要的值都已获取，可以提前退出
                    if (memInfo.MemTotal > 0 && memInfo.MemFree >= 0 && memInfo.Buffers >= 0 && memInfo.Cached >= 0)
                        break;
                }
                
                _logger.LogDebug($"读取内存信息 - 总内存: {memInfo.MemTotal}, 可用: {memInfo.MemFree}, 缓冲区: {memInfo.Buffers}, 缓存: {memInfo.Cached}");
                
                return memInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取 /proc/meminfo 失败");
                return null;
            }
        }

        /// <summary>
        /// 获取适合当前平台的磁盘路径
        /// </summary>
        private string GetPlatformSpecificDiskPath(string diskName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(diskName) || diskName.ToLower() == "auto")
                {
                    // 自动检测主磁盘
                    if (_isWindows)
                    {
                        return "C:\\"; // Windows 默认系统盘
                    }
                    else if (_isLinux)
                    {
                        return "/"; // Linux 根目录
                    }
                    else
                    {
                        return "/"; // 其他系统默认根目录
                    }
                }
                
                // 使用配置指定的磁盘名称
                if (_isWindows)
                {
                    // Windows: 确保格式为 "C:\\" 
                    if (diskName.Length == 1 && char.IsLetter(diskName[0]))
                    {
                        return $"{diskName.ToUpper()}:\\";
                    }
                    else if (diskName.EndsWith(":"))
                    {
                        return diskName + "\\";
                    }
                    else if (!diskName.EndsWith("\\"))
                    {
                        return diskName + "\\";
                    }
                    return diskName;
                }
                else
                {
                    // Linux/Unix: 使用原始路径
                    return diskName.StartsWith("/") ? diskName : "/" + diskName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"解析磁盘路径失败: {diskName}，使用默认路径");
                return _isWindows ? "C:\\" : "/";
            }
        }

        /// <summary>
        /// 跨平台获取磁盘使用情况
        /// </summary>
        private async Task<(double UsagePercentage, long TotalBytes, long UsedBytes, long FreeBytes)> GetDiskUsageAsync(string path)
        {
            try
            {
                if (_isWindows)
                {
                    return await GetWindowsDiskUsageAsync(path);
                }
                else if (_isLinux)
                {
                    return await GetLinuxDiskUsageAsync(path);
                }
                else
                {
                    _logger.LogWarning("不支持的操作系统，无法获取磁盘使用情况");
                    return (0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取磁盘使用情况失败: {path}");
                return (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Windows 系统磁盘使用情况
        /// </summary>
        private async Task<(double UsagePercentage, long TotalBytes, long UsedBytes, long FreeBytes)> GetWindowsDiskUsageAsync(string driveName)
        {
            try
            {
                _logger.LogDebug($"尝试获取Windows磁盘使用情况: {driveName}");
                
                var drive = new DriveInfo(driveName);
                if (drive.IsReady)
                {
                    var totalBytes = drive.TotalSize;
                    var freeBytes = drive.AvailableFreeSpace;
                    var usedBytes = totalBytes - freeBytes;
                    var usagePercentage = (double)usedBytes / totalBytes * 100;
                    
                    _logger.LogDebug($"磁盘 {driveName} - 总计: {totalBytes / (1024 * 1024 * 1024):F2}GB, 已用: {usedBytes / (1024 * 1024 * 1024):F2}GB, 使用率: {usagePercentage:F2}%");
                    
                    return (Math.Round(usagePercentage, 2), totalBytes, usedBytes, freeBytes);
                }
                else
                {
                    _logger.LogWarning($"磁盘 {driveName} 未就绪");
                    return (0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取Windows磁盘使用情况失败: {driveName}");
                return (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Linux 系统磁盘使用情况
        /// </summary>
        private async Task<(double UsagePercentage, long TotalBytes, long UsedBytes, long FreeBytes)> GetLinuxDiskUsageAsync(string path)
        {
            try
            {
                _logger.LogDebug($"尝试获取Linux磁盘使用情况: {path}");
                
                if (!Directory.Exists(path))
                {
                    _logger.LogWarning($"路径不存在: {path}");
                    return (0, 0, 0, 0);
                }
                
                var driveInfo = new DriveInfo(path);
                if (driveInfo.IsReady)
                {
                    var totalBytes = driveInfo.TotalSize;
                    var freeBytes = driveInfo.AvailableFreeSpace;
                    var usedBytes = totalBytes - freeBytes;
                    var usagePercentage = (double)usedBytes / totalBytes * 100;
                    
                    _logger.LogDebug($"磁盘 {path} - 总计: {totalBytes / (1024 * 1024 * 1024):F2}GB, 已用: {usedBytes / (1024 * 1024 * 1024):F2}GB, 使用率: {usagePercentage:F2}%");
                    
                    return (Math.Round(usagePercentage, 2), totalBytes, usedBytes, freeBytes);
                }
                else
                {
                    _logger.LogWarning($"磁盘 {path} 未就绪");
                    return (0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取Linux磁盘使用情况失败: {path}");
                return (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// 读取 /proc/stat 文件获取 CPU 统计信息（优化版本）
        /// </summary>
        private async Task<CpuStat?> ReadProcStatAsync()
        {
            try
            {
                if (!File.Exists("/proc/stat"))
                {
                    _logger.LogWarning("/proc/stat 文件不存在");
                    return null;
                }
                
                var lines = await File.ReadAllLinesAsync("/proc/stat");
                if (lines.Length == 0)
                {
                    _logger.LogWarning("/proc/stat 文件为空");
                    return null;
                }
                
                // 第一行包含总体CPU统计信息
                var cpuLine = lines[0];
                if (!cpuLine.StartsWith("cpu "))
                {
                    _logger.LogWarning("/proc/stat 格式不正确");
                    return null;
                }
                
                // 解析CPU统计数据: cpu user nice system idle iowait irq softirq
                var parts = cpuLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 8)
                {
                    _logger.LogWarning($"/proc/stat CPU行格式不正确: {cpuLine}");
                    return null;
                }
                
                var cpuStat = new CpuStat
                {
                    User = long.Parse(parts[1]),
                    Nice = long.Parse(parts[2]),
                    System = long.Parse(parts[3]),
                    Idle = long.Parse(parts[4]),
                    IOWait = parts.Length > 5 ? long.Parse(parts[5]) : 0,
                    IRQ = parts.Length > 6 ? long.Parse(parts[6]) : 0,
                    SoftIRQ = parts.Length > 7 ? long.Parse(parts[7]) : 0
                };
                
                _logger.LogDebug($"读取CPU统计 - User: {cpuStat.User}, System: {cpuStat.System}, Idle: {cpuStat.Idle}, IOWait: {cpuStat.IOWait}");
                
                return cpuStat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取 /proc/stat 失败");
                return null;
            }
        }

        /// <summary>
        /// CPU 统计信息结构
        /// </summary>
        private class CpuStat
        {
            public long User { get; set; }
            public long Nice { get; set; }
            public long System { get; set; }
            public long Idle { get; set; }
            public long IOWait { get; set; }
            public long IRQ { get; set; }
            public long SoftIRQ { get; set; }
        }

        /// <summary>
        /// 内存信息结构
        /// </summary>
        private class MemInfo
        {
            public long MemTotal { get; set; }
            public long MemFree { get; set; }
            public long Buffers { get; set; }
            public long Cached { get; set; }
        }
    }
}