using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Cronos;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 定时任务 DTO
    /// </summary>
    public class ScheduledTaskDto
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "任务名称不能为空")]
        [MaxLength(100, ErrorMessage = "任务名称长度不能超过100个字符")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "任务编码不能为空")]
        [MaxLength(100, ErrorMessage = "任务编码长度不能超过100个字符")]
        public string Code { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Cron表达式不能为空")]
        [MaxLength(50, ErrorMessage = "Cron表达式长度不能超过50个字符")]
        public string Cron { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "执行处理器不能为空")]
        [MaxLength(255, ErrorMessage = "执行处理器长度不能超过255个字符")]
        public string Handler { get; set; } = string.Empty;
        
        public string? Parameters { get; set; }
        
        public bool Enabled { get; set; } = true;
        
        public DateTime? LastRunTime { get; set; }
        
        public DateTime? NextRuntime { get; set; }
        
        /// <summary>
        /// 最近一次执行耗时（毫秒）
        /// </summary>
        public long? Duration { get; set; }
        
        [MaxLength(255, ErrorMessage = "备注长度不能超过255个字符")]
        public string? Remark { get; set; }
        
        public DateTime CrtTime { get; set; }
        
        public DateTime UpdTime { get; set; }
    }
    
    /// <summary>
    /// 定时任务日志 DTO
    /// </summary>
    public class ScheduledTaskLogDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long? Duration => EndTime.HasValue ? (long)(EndTime.Value - StartTime).TotalMilliseconds : (long?)null;
    }

/// <summary>
    /// 定时任务分页响应模型
    /// </summary>
    public class ScheduledTaskPagedResponse
    {
        public List<ScheduledTaskDto> Items { get; set; } = new List<ScheduledTaskDto>();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// 定时任务日志分页响应模型
    /// </summary>
    public class ScheduledTaskLogPagedResponse
    {
        public List<ScheduledTaskLog> Items { get; set; } = new List<ScheduledTaskLog>();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// 批量删除请求模型
    /// </summary>
    public class BatchDeleteTaskRequest
    {
        public int[] Ids { get; set; } = new int[0];
    }

    /// <summary>
    /// 批量删除结果模型
    /// </summary>
    public class BatchDeleteTaskResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> FailedReasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// 任务启用/禁用请求模型
    /// </summary>
    public class TaskEnabledRequest
    {
        public bool Enabled { get; set; }
    }
}