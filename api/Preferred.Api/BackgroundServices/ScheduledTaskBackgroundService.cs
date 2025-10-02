using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Preferred.Api.Models;
using Preferred.Api.Services;
using Cronos;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 定时任务后台服务
    /// </summary>
    public class ScheduledTaskBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledTaskBackgroundService> _logger;
        private readonly Dictionary<int, Timer> _taskTimers = new Dictionary<int, Timer>();
        private readonly object _lockObject = new object();

        public ScheduledTaskBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ScheduledTaskBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("定时任务后台服务已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshScheduledTasksAsync();
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // 每分钟检查一次
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "定时任务后台服务执行异常");
                }
            }
        }

        /// <summary>
        /// 刷新定时任务
        /// </summary>
        private async Task RefreshScheduledTasksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();

            var tasks = await taskService.GetAllTasksAsync();
            var enabledTasks = tasks.Where(t => t.Enabled).ToList();

            lock (_lockObject)
            {
                // 停止已删除或禁用的任务
                var tasksToRemove = _taskTimers.Keys.Except(enabledTasks.Select(t => t.Id)).ToList();
                foreach (var taskId in tasksToRemove)
                {
                    _taskTimers[taskId]?.Dispose();
                    _taskTimers.Remove(taskId);
                    _logger.LogInformation($"停止定时任务: {taskId}");
                }

                // 启动新的或更新的任务
                foreach (var task in enabledTasks)
                {
                    if (!_taskTimers.ContainsKey(task.Id))
                    {
                        StartTask(task);
                    }
                }
            }
        }

        /// <summary>
        /// 启动定时任务
        /// </summary>
        private void StartTask(ScheduledTaskDto task)
        {
            try
            {
                // 转换 Quartz 风格的 Cron 表达式为标准格式
                var standardCron = ConvertQuartzCronToStandard(task.Cron);
                var cron = CronExpression.Parse(standardCron);
                // 使用 UTC 时间
                var nextRun = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Local);

                if (nextRun.HasValue)
                {
                    var delay = nextRun.Value - DateTime.UtcNow;
                    if (delay.TotalMilliseconds > 0)
                    {
                        var timer = new Timer(async _ => await ExecuteTaskAsync(task.Id), null, delay, Timeout.InfiniteTimeSpan);
                        _taskTimers[task.Id] = timer;
                        _logger.LogInformation($"定时任务 {task.Name} 已安排在 {nextRun.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss} 执行");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"启动定时任务失败: {task.Name}");
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
        /// 执行定时任务
        /// </summary>
        private async Task ExecuteTaskAsync(int taskId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var taskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();

                _logger.LogInformation($"开始执行定时任务: {taskId}");
                var success = await taskService.ExecuteTaskAsync(taskId);
                _logger.LogInformation($"定时任务执行完成: {taskId}, 结果: {(success ? "成功" : "失败")}");

                // 重新安排下次执行
                var task = await taskService.GetTaskByIdAsync(taskId);
                if (task?.Enabled == true)
                {
                    lock (_lockObject)
                    {
                        _taskTimers.Remove(taskId);
                        StartTask(task);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行定时任务异常: {taskId}");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("定时任务后台服务正在停止...");

            lock (_lockObject)
            {
                foreach (var timer in _taskTimers.Values)
                {
                    timer?.Dispose();
                }
                _taskTimers.Clear();
            }

            await base.StopAsync(stoppingToken);
            _logger.LogInformation("定时任务后台服务已停止");
        }

        public override void Dispose()
        {
            lock (_lockObject)
            {
                foreach (var timer in _taskTimers.Values)
                {
                    timer?.Dispose();
                }
                _taskTimers.Clear();
            }
            base.Dispose();
        }
    }
}