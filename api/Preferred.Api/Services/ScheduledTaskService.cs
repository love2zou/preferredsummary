using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private const string SystemMonitorTaskKey = "systemmonitor";
        private const string LogCleanupTaskKey = "logcleanup";
        private const string DataBackupTaskKey = "databackup";

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
            var taskExecutionKey = ResolveTaskExecutionKey(task);

            _logger.LogInformation(
                "定时任务匹配结果: Task={TaskName}({TaskCode}), Handler={Handler}, ExecutionKey={ExecutionKey}",
                task.Name,
                task.Code,
                task.Handler,
                taskExecutionKey);

            switch (taskExecutionKey)
            {
                case SystemMonitorTaskKey:
                    await ExecuteSystemMonitorTaskAsync(task);
                    break;
                case LogCleanupTaskKey:
                    await ExecuteLogCleanupTaskAsync(task);
                    break;
                case DataBackupTaskKey:
                    await ExecuteDataBackupTaskAsync(task);
                    break;
                default:
                    throw new NotSupportedException($"未识别的任务处理器: Handler={task.Handler}, Code={task.Code}, Name={task.Name}");
            }
        }

        private static string ResolveTaskExecutionKey(ScheduledTask task)
        {
            var candidates = new[]
            {
                NormalizeTaskToken(task.Handler),
                NormalizeTaskToken(task.Code),
                NormalizeTaskToken(task.Name)
            }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            if (candidates.Any(candidate => MatchesTaskAlias(candidate,
                    "Preferred.Api.Services.SystemMonitorService.MonitorAndSaveAsync",
                    "SYSTEM_RESOURCE_MONITOR",
                    "SystemMonitor",
                    "MonitorAndSaveAsync")))
            {
                return SystemMonitorTaskKey;
            }

            if (candidates.Any(candidate => MatchesTaskAlias(candidate,
                    "logcleanup",
                    "LOG_CLEANUP",
                    "ExecuteLogCleanupTaskAsync",
                    "CleanExpiredLogsAsync",
                    "日志清理",
                    "清理日志")))
            {
                return LogCleanupTaskKey;
            }

            if (candidates.Any(candidate => MatchesTaskAlias(candidate,
                    "databackup",
                    "DATA_BACKUP",
                    "databasebackup",
                    "ExecuteDataBackupTaskAsync",
                    "数据备份",
                    "备份数据")))
            {
                return DataBackupTaskKey;
            }

            return NormalizeTaskToken(task.Handler);
        }

        private static bool MatchesTaskAlias(string normalizedCandidate, params string[] aliases)
        {
            return aliases.Select(NormalizeTaskToken).Any(alias => alias == normalizedCandidate);
        }

        private static string NormalizeTaskToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
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
            
            var days = ParseTaskParameterInt(task.Parameters, "days", 30);
            var (dbLogCount, fileCount) = await CleanExpiredLogsInternalAsync(days);
            _logger.LogInformation(
                "日志清理完成: Task={TaskName}, 保留天数={Days}, 清理任务日志={DbLogCount}, 清理日志文件={FileCount}",
                task.Name,
                days,
                dbLogCount,
                fileCount);
        }
        
        /// <summary>
        /// 执行数据备份任务
        /// </summary>
        private async Task ExecuteDataBackupTaskAsync(ScheduledTask task)
        {
            _logger.LogInformation($"执行数据备份任务: {task.Name}");

            var backupDirectory = ResolveBackupDirectory(task.Parameters);
            Directory.CreateDirectory(backupDirectory);

            var now = DateTime.Now;
            var fileName = $"backup-{now:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(backupDirectory, fileName);

            var backupPayload = new
            {
                createdAt = now,
                task = new
                {
                    task.Id,
                    task.Name,
                    task.Code,
                    task.Handler,
                    task.Parameters
                },
                summary = new
                {
                    users = await _context.Users.AsNoTracking().CountAsync(),
                    categories = await _context.Categories.AsNoTracking().CountAsync(),
                    tags = await _context.Tags.AsNoTracking().CountAsync(),
                    pictures = await _context.Pictures.AsNoTracking().CountAsync(),
                    networkUrls = await _context.NetworkUrls.AsNoTracking().CountAsync(),
                    files = await _context.FileRecords.AsNoTracking().CountAsync(),
                    notifications = await _context.Notifications.AsNoTracking().CountAsync(),
                    systemResources = await _context.SystemResources.AsNoTracking().CountAsync(),
                    scheduledTasks = await _context.ScheduledTasks.AsNoTracking().CountAsync(),
                    scheduledTaskLogs = await _context.ScheduledTaskLogs.AsNoTracking().CountAsync()
                },
                data = new
                {
                    scheduledTasks = await _context.ScheduledTasks.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToListAsync(),
                    categories = await _context.Categories.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToListAsync(),
                    tags = await _context.Tags.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToListAsync(),
                    pictures = await _context.Pictures.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToListAsync(),
                    networkUrls = await _context.NetworkUrls.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToListAsync(),
                    fileRecords = await _context.FileRecords.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToListAsync(),
                    notifications = await _context.Notifications.AsNoTracking()
                        .OrderByDescending(x => x.Id)
                        .Take(500)
                        .ToListAsync(),
                    systemResources = await _context.SystemResources.AsNoTracking()
                        .OrderByDescending(x => x.Id)
                        .Take(1000)
                        .ToListAsync(),
                    scheduledTaskLogs = await _context.ScheduledTaskLogs.AsNoTracking()
                        .OrderByDescending(x => x.Id)
                        .Take(1000)
                        .ToListAsync()
                }
            };

            var json = JsonSerializer.Serialize(backupPayload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            CleanupExpiredBackupFiles(backupDirectory, ParseTaskParameterInt(task.Parameters, "maxFiles", 10));

            _logger.LogInformation("数据备份完成: Task={TaskName}, File={FilePath}", task.Name, filePath);
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
            var (dbLogCount, fileCount) = await CleanExpiredLogsInternalAsync(days);
            return dbLogCount + fileCount;
        }

        private async Task<(int DbLogCount, int FileCount)> CleanExpiredLogsInternalAsync(int days)
        {
            var expiredDate = DateTime.Now.AddDays(-days);
            
            var expiredLogs = await _context.ScheduledTaskLogs
                .Where(x => x.CrtTime < expiredDate)
                .ToListAsync();

            var dbLogCount = expiredLogs.Count;
                
            if (expiredLogs.Any())
            {
                _context.ScheduledTaskLogs.RemoveRange(expiredLogs);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"清理过期任务日志 {expiredLogs.Count} 条");
            }
            
            var fileCount = CleanExpiredApplicationLogFiles(expiredDate);
            return (dbLogCount, fileCount);
        }

        private int CleanExpiredApplicationLogFiles(DateTime expiredDate)
        {
            var deletedCount = 0;
            var logDirectories = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "logs"),
                Path.Combine(AppContext.BaseDirectory, "logs")
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);

            foreach (var logDirectory in logDirectories)
            {
                foreach (var logFile in Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFile);
                        if (fileInfo.LastWriteTime >= expiredDate)
                        {
                            continue;
                        }

                        fileInfo.Delete();
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除过期日志文件失败: {LogFile}", logFile);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("清理过期应用日志文件 {DeletedCount} 个", deletedCount);
            }

            return deletedCount;
        }

        private static int ParseTaskParameterInt(string? parameters, string propertyName, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                return defaultValue;
            }

            if (int.TryParse(parameters, out var directValue))
            {
                return directValue;
            }

            try
            {
                using var document = JsonDocument.Parse(parameters);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(propertyName, out var property))
                {
                    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numberValue))
                    {
                        return numberValue;
                    }

                    if (property.ValueKind == JsonValueKind.String
                        && int.TryParse(property.GetString(), out var stringValue))
                    {
                        return stringValue;
                    }
                }
            }
            catch
            {
                // Ignore malformed JSON parameters and use defaults.
            }

            return defaultValue;
        }

        private static string ResolveBackupDirectory(string? parameters)
        {
            const string defaultDirectoryName = "backups";

            if (string.IsNullOrWhiteSpace(parameters))
            {
                return Path.Combine(Directory.GetCurrentDirectory(), defaultDirectoryName);
            }

            try
            {
                using var document = JsonDocument.Parse(parameters);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("directory", out var directoryProperty)
                    && directoryProperty.ValueKind == JsonValueKind.String)
                {
                    var configuredDirectory = directoryProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(configuredDirectory))
                    {
                        return Path.IsPathRooted(configuredDirectory)
                            ? configuredDirectory
                            : Path.Combine(Directory.GetCurrentDirectory(), configuredDirectory);
                    }
                }
            }
            catch
            {
                // Ignore malformed JSON parameters and use defaults.
            }

            return Path.Combine(Directory.GetCurrentDirectory(), defaultDirectoryName);
        }

        private void CleanupExpiredBackupFiles(string backupDirectory, int maxFilesToKeep)
        {
            if (maxFilesToKeep <= 0 || !Directory.Exists(backupDirectory))
            {
                return;
            }

            var backupFiles = new DirectoryInfo(backupDirectory)
                .GetFiles("backup-*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(x => x.CreationTimeUtc)
                .ToList();

            foreach (var expiredFile in backupFiles.Skip(maxFilesToKeep))
            {
                try
                {
                    expiredFile.Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除旧备份文件失败: {BackupFile}", expiredFile.FullName);
                }
            }
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
