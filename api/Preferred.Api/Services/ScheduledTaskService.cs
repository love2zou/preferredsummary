using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Cronos;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 定时任务服务实现
    /// </summary>
    public class ScheduledTaskService : IScheduledTaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScheduledTaskService> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        public ScheduledTaskService(ApplicationDbContext context, ILogger<ScheduledTaskService> logger, IServiceProvider serviceProvider)
        {
            _context = context;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        
        /// <summary>
        /// 获取所有定时任务
        /// </summary>
        public async Task<List<ScheduledTaskDto>> GetAllTasksAsync()
        {
            var tasks = await _context.ScheduledTasks
                .OrderBy(x => x.Name)
                .ToListAsync();
                
            var taskDtos = new List<ScheduledTaskDto>();
            
            foreach (var task in tasks)
            {
                var dto = await MapToDtoWithExtendedInfoAsync(task);
                taskDtos.Add(dto);
            }
            
            return taskDtos;
        }
        
        /// <summary>
        /// 实体转DTO（包含扩展信息）
        /// </summary>
        private async Task<ScheduledTaskDto> MapToDtoWithExtendedInfoAsync(ScheduledTask task)
        {
            var dto = MapToDto(task);
            
            // 计算下次执行时间（只有启用状态才计算）
            dto.NextRuntime = task.Enabled ? CalculateNextRunTime(task.Cron) : null;
            
            // 获取最近一次执行耗时
            dto.Duration = await GetLatestExecutionDurationAsync(task.Id);
            
            return dto;
        }
        
        /// <summary>
        /// 计算下次执行时间
        /// </summary>
        private DateTime? CalculateNextRunTime(string cronExpression)
        {
            try
            {
                // 转换 Quartz 风格的 Cron 表达式
                var standardCron = ConvertQuartzCronToStandard(cronExpression);
                var cron = CronExpression.Parse(standardCron);
                return cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Local);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"解析Cron表达式失败: {cronExpression}");
                return null;
            }
        }
        
        /// <summary>
        /// 转换 Quartz 风格的 Cron 表达式为标准格式
        /// </summary>
        private string ConvertQuartzCronToStandard(string quartzCron)
        {
            // Quartz: 秒 分 时 日 月 周 [年]
            // Standard: 分 时 日 月 周
            var parts = quartzCron.Split(' ');
            if (parts.Length >= 6)
            {
                // 移除秒字段，将 ? 替换为 *
                var standardParts = parts.Skip(1).Take(5)
                    .Select(p => p == "?" ? "*" : p)
                    .ToArray();
                return string.Join(" ", standardParts);
            }
            return quartzCron.Replace("?", "*");
        }
        
        /// <summary>
        /// 获取最近一次执行耗时
        /// </summary>
        private async Task<long?> GetLatestExecutionDurationAsync(int taskId)
        {
            var latestLog = await _context.ScheduledTaskLogs
                .Where(x => x.TaskId == taskId && x.EndTime.HasValue)
                .OrderByDescending(x => x.StartTime)
                .FirstOrDefaultAsync();
                
            if (latestLog?.EndTime.HasValue == true)
            {
                return (long)(latestLog.EndTime.Value - latestLog.StartTime).TotalMilliseconds;
            }
            
            return null;
        }
        
        /// <summary>
        /// 根据ID获取定时任务
        /// </summary>
        public async Task<ScheduledTaskDto?> GetTaskByIdAsync(int id)
        {
            var task = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Id == id);
                
            return task != null ? MapToDto(task) : null;
        }
        
        /// <summary>
        /// 根据编码获取定时任务
        /// </summary>
        public async Task<ScheduledTaskDto?> GetTaskByCodeAsync(string code)
        {
            var task = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Code == code);
                
            return task != null ? MapToDto(task) : null;
        }
        
        /// <summary>
        /// 创建定时任务
        /// </summary>
        public async Task<ScheduledTaskDto> CreateTaskAsync(ScheduledTaskDto taskDto)
        {
            // 检查编码是否已存在
            var existingTask = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Code == taskDto.Code);
            if (existingTask != null)
            {
                throw new InvalidOperationException($"任务编码 '{taskDto.Code}' 已存在");
            }
            
            var task = new ScheduledTask
            {
                Name = taskDto.Name,
                Code = taskDto.Code,
                Cron = taskDto.Cron,
                Handler = taskDto.Handler,
                Parameters = taskDto.Parameters,
                Enabled = taskDto.Enabled,
                Remark = taskDto.Remark,
                CrtTime = DateTime.Now,
                UpdTime = DateTime.Now
            };
            
            _context.ScheduledTasks.Add(task);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"创建定时任务成功: {task.Name} ({task.Code})");
            
            return MapToDto(task);
        }
        
        /// <summary>
        /// 更新定时任务
        /// </summary>
        public async Task<ScheduledTaskDto?> UpdateTaskAsync(int id, ScheduledTaskDto taskDto)
        {
            var task = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Id == id);
            if (task == null)
            {
                return null;
            }
            
            // 检查编码是否与其他任务冲突
            var existingTask = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Code == taskDto.Code && x.Id != id);
            if (existingTask != null)
            {
                throw new InvalidOperationException($"任务编码 '{taskDto.Code}' 已存在");
            }
            
            task.Name = taskDto.Name;
            task.Code = taskDto.Code;
            task.Cron = taskDto.Cron;
            task.Handler = taskDto.Handler;
            task.Parameters = taskDto.Parameters;
            task.Enabled = taskDto.Enabled;
            task.Remark = taskDto.Remark;
            task.UpdTime = DateTime.Now;
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"更新定时任务成功: {task.Name} ({task.Code})");
            
            return MapToDto(task);
        }
        
        /// <summary>
        /// 删除定时任务
        /// </summary>
        public async Task<bool> DeleteTaskAsync(int id)
        {
            var task = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Id == id);
            if (task == null)
            {
                return false;
            }
            
            // 删除相关日志
            var logs = await _context.ScheduledTaskLogs
                .Where(x => x.TaskId == id)
                .ToListAsync();
            _context.ScheduledTaskLogs.RemoveRange(logs);
            
            _context.ScheduledTasks.Remove(task);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"删除定时任务成功: {task.Name} ({task.Code})");
            
            return true;
        }
        
        /// <summary>
        /// 启用/禁用定时任务
        /// </summary>
        public async Task<bool> SetTaskEnabledAsync(int id, bool enabled)
        {
            var task = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Id == id);
            if (task == null)
            {
                return false;
            }
            
            task.Enabled = enabled;
            task.UpdTime = DateTime.Now;
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"{(enabled ? "启用" : "禁用")}定时任务: {task.Name} ({task.Code})");
            
            return true;
        }
        
        /// <summary>
        /// 手动执行定时任务
        /// </summary>
        public async Task<bool> ExecuteTaskAsync(int id)
        {
            var task = await _context.ScheduledTasks
                .FirstOrDefaultAsync(x => x.Id == id);
            if (task == null)
            {
                return false;
            }
            
            var startTime = DateTime.Now;
            var log = new ScheduledTaskLog
            {
                TaskId = task.Id,
                TaskCode = task.Code,
                StartTime = startTime,
                Success = false,
                Message = "任务开始执行",
                CrtTime = startTime,
                UpdTime = startTime
            };
            
            // 先保存开始日志
            _context.ScheduledTaskLogs.Add(log);
            await _context.SaveChangesAsync();
            
            try
            {
                _logger.LogInformation($"开始手动执行定时任务: {task.Name} ({task.Code})");
                
                // TODO: 根据 Handler 类型执行不同的任务逻辑
                // 这里可以扩展为工厂模式，根据 Handler 创建不同的执行器
                await ExecuteTaskByHandlerAsync(task);
                
                log.EndTime = DateTime.Now;
                log.Success = true;
                log.Message = $"手动执行成功，耗时：{(log.EndTime.Value - log.StartTime).TotalSeconds:F2}秒";
                log.UpdTime = DateTime.Now;
                
                // 更新任务最后运行时间
                task.LastRunTime = startTime;
                task.UpdTime = DateTime.Now;
                
                _logger.LogInformation($"手动执行定时任务成功: {task.Name} ({task.Code}), 耗时: {(log.EndTime.Value - log.StartTime).TotalSeconds:F2}秒");
            }
            catch (Exception ex)
            {
                log.EndTime = DateTime.Now;
                log.Success = false;
                log.Message = $"执行失败: {ex.Message}";
                log.UpdTime = DateTime.Now;
                
                _logger.LogError(ex, $"手动执行定时任务失败: {task.Name} ({task.Code})");
            }
            
            // 更新日志
            await _context.SaveChangesAsync();
            
            return log.Success;
        }
        
        /// <summary>
        /// 根据处理器类型执行任务
        /// </summary>
        private async Task ExecuteTaskByHandlerAsync(ScheduledTask task)
        {
            switch (task.Handler)
            {
                case "Preferred.Api.Services.SystemMonitorService.MonitorAndSaveAsync":
                    // 执行系统监控任务
                    await ExecuteSystemMonitorTaskAsync(task);
                    break;
                case "logcleanup":
                    // 执行日志清理任务
                    await ExecuteLogCleanupTaskAsync(task);
                    break;
                case "databackup":
                    // 执行数据备份任务
                    await ExecuteDataBackupTaskAsync(task);
                    break;
                default:
                    // 默认执行逻辑
                    await Task.Delay(1000); // 模拟执行
                    break;
            }
        }
        
        /// <summary>
        /// 执行系统监控任务
        /// </summary>
        private async Task ExecuteSystemMonitorTaskAsync(ScheduledTask task)
        {
            try
            {
                _logger.LogInformation($"执行系统监控任务: {task.Name}");
                
                using var scope = _serviceProvider.CreateScope();
                var systemMonitorService = scope.ServiceProvider.GetRequiredService<ISystemMonitorService>();
                await systemMonitorService.MonitorAndSaveAsync();
                
                _logger.LogInformation($"系统监控任务执行完成: {task.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行系统监控任务失败: {task.Name}");
                throw;
            }
        }
        
        /// <summary>
        /// 执行日志清理任务
        /// </summary>
        private async Task ExecuteLogCleanupTaskAsync(ScheduledTask task)
        {
            _logger.LogInformation($"执行日志清理任务: {task.Name}");
            
            // 解析参数，获取保留天数
            var days = 30; // 默认30天
            if (!string.IsNullOrEmpty(task.Parameters))
            {
                if (int.TryParse(task.Parameters, out var parsedDays))
                {
                    days = parsedDays;
                }
            }
            
            var cleanedCount = await CleanExpiredLogsAsync(days);
            _logger.LogInformation($"日志清理完成，清理了 {cleanedCount} 条过期日志");
        }
        
        /// <summary>
        /// 执行数据备份任务
        /// </summary>
        private async Task ExecuteDataBackupTaskAsync(ScheduledTask task)
        {
            _logger.LogInformation($"执行数据备份任务: {task.Name}");
            // TODO: 实现数据备份逻辑
            await Task.Delay(2000); // 模拟执行
        }
        
        /// <summary>
        /// 获取任务执行日志
        /// </summary>
        public async Task<(List<ScheduledTaskLogDto> logs, int total)> GetTaskLogsAsync(
            int? taskId, 
            bool? success, 
            DateTime? startTime, 
            DateTime? endTime, 
            int pageIndex, 
            int pageSize)
        {
            var query = _context.ScheduledTaskLogs.AsQueryable();
            
            if (taskId.HasValue)
            {
                query = query.Where(x => x.TaskId == taskId.Value);
            }
            
            if (success.HasValue)
            {
                query = query.Where(x => x.Success == success.Value);
            }
            
            if (startTime.HasValue)
            {
                query = query.Where(x => x.StartTime >= startTime.Value);
            }
            
            if (endTime.HasValue)
            {
                query = query.Where(x => x.StartTime <= endTime.Value);
            }
            
            var total = await query.CountAsync();
            
            var logs = await query
                .OrderByDescending(x => x.StartTime)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            var logDtos = logs.Select(log => new ScheduledTaskLogDto
            {
                Id = log.Id,
                TaskId = log.TaskId,
                TaskCode = log.TaskCode,
                TaskName = "", // 需要关联查询任务名称
                StartTime = log.StartTime,
                EndTime = log.EndTime,
                Success = log.Success,
                Message = log.Message,
                CrtTime = log.CrtTime,
                UpdTime = log.UpdTime
            }).ToList();
            
            // 填充任务名称
            var taskIds = logDtos.Select(x => x.TaskId).Distinct().ToList();
            var tasks = await _context.ScheduledTasks
                .Where(x => taskIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name);
                
            foreach (var logDto in logDtos)
            {
                if (tasks.TryGetValue(logDto.TaskId, out var taskName))
                {
                    logDto.TaskName = taskName;
                }
            }
            
            return (logDtos, total);
        }
        
        /// <summary>
        /// 清理过期日志
        /// </summary>
        public async Task<int> CleanExpiredLogsAsync(int days = 30)
        {
            var expiredDate = DateTime.Now.AddDays(-days);
            
            var expiredLogs = await _context.ScheduledTaskLogs
                .Where(x => x.CrtTime < expiredDate)
                .ToListAsync();
                
            if (expiredLogs.Any())
            {
                _context.ScheduledTaskLogs.RemoveRange(expiredLogs);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"清理过期日志 {expiredLogs.Count} 条");
            }
            
            return expiredLogs.Count;
        }
        
        /// <summary>
        /// 实体转DTO
        /// </summary>
        private static ScheduledTaskDto MapToDto(ScheduledTask task)
        {
            return new ScheduledTaskDto
            {
                Id = task.Id,
                Name = task.Name,
                Code = task.Code,
                Cron = task.Cron,
                Handler = task.Handler,
                Parameters = task.Parameters,
                Enabled = task.Enabled,
                LastRunTime = task.LastRunTime,
                NextRuntime = task.NextRuntime,
                Remark = task.Remark,
                CrtTime = task.CrtTime,
                UpdTime = task.UpdTime
            };
        }
        
        /// <summary>
        /// 批量删除定时任务
        /// </summary>
        public async Task<BatchDeleteTaskResult> BatchDeleteTasksAsync(int[] ids)
        {
            var result = new BatchDeleteTaskResult
            {
                SuccessCount = 0,
                FailCount = 0,
                FailedReasons = new List<string>()
            };
            
            foreach (var id in ids)
            {
                try
                {
                    var success = await DeleteTaskAsync(id);
                    if (success)
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailCount++;
                        result.FailedReasons.Add($"任务ID {id} 不存在");
                    }
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.FailedReasons.Add($"删除任务ID {id} 失败: {ex.Message}");
                }
            }
            
            _logger.LogInformation($"批量删除定时任务完成，成功：{result.SuccessCount}，失败：{result.FailCount}");
            
            return result;
        }
    }
}