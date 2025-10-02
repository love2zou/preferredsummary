using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 定时任务服务接口
    /// </summary>
    public interface IScheduledTaskService
    {
        /// <summary>
        /// 获取所有定时任务
        /// </summary>
        /// <returns>定时任务列表</returns>
        Task<List<ScheduledTaskDto>> GetAllTasksAsync();
        
        /// <summary>
        /// 根据ID获取定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>定时任务信息</returns>
        Task<ScheduledTaskDto?> GetTaskByIdAsync(int id);
        
        /// <summary>
        /// 根据编码获取定时任务
        /// </summary>
        /// <param name="code">任务编码</param>
        /// <returns>定时任务信息</returns>
        Task<ScheduledTaskDto?> GetTaskByCodeAsync(string code);
        
        /// <summary>
        /// 创建定时任务
        /// </summary>
        /// <param name="taskDto">任务信息</param>
        /// <returns>创建的任务信息</returns>
        Task<ScheduledTaskDto> CreateTaskAsync(ScheduledTaskDto taskDto);
        
        /// <summary>
        /// 更新定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="taskDto">任务信息</param>
        /// <returns>更新的任务信息</returns>
        Task<ScheduledTaskDto?> UpdateTaskAsync(int id, ScheduledTaskDto taskDto);
        
        /// <summary>
        /// 删除定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteTaskAsync(int id);
        
        /// <summary>
        /// 启用/禁用定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>是否操作成功</returns>
        Task<bool> SetTaskEnabledAsync(int id, bool enabled);
        
        /// <summary>
        /// 手动执行定时任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>是否执行成功</returns>
        Task<bool> ExecuteTaskAsync(int id);
        
        /// <summary>
        /// 获取任务执行日志
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="success">执行结果</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="pageSize">页大小</param>
        /// <returns>执行日志列表</returns>
        Task<(List<ScheduledTaskLogDto> logs, int total)> GetTaskLogsAsync(
            int? taskId, 
            bool? success, 
            DateTime? startTime, 
            DateTime? endTime, 
            int pageIndex, 
            int pageSize);
        
        /// <summary>
        /// 清理过期日志
        /// </summary>
        /// <param name="days">保留天数</param>
        /// <returns>清理的日志数量</returns>
        Task<int> CleanExpiredLogsAsync(int days = 30);
        
        /// <summary>
        /// 批量删除定时任务
        /// </summary>
        /// <param name="ids">任务ID数组</param>
        /// <returns>批量删除结果</returns>
        Task<BatchDeleteTaskResult> BatchDeleteTasksAsync(int[] ids);
    }
}