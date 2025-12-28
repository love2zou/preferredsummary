using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 数据解析请求模型
    /// </summary>
    public class DataParseRequest
    {
        /// <summary>
        /// 文件名
        /// </summary>
        [Required]
        public string FileName { get; set; }
        
        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }
    }

    /// <summary>
    /// 数据解析响应模型
    /// </summary>
    public class DataParseResponse
    {
        /// <summary>
        /// 解析任务ID
        /// </summary>
        public string TaskId { get; set; }
        
        /// <summary>
        /// 解析状态
        /// </summary>
        public ParseStatus Status { get; set; }
        
        /// <summary>
        /// 解析进度（0-100）
        /// </summary>
        public int Progress { get; set; }
        
        /// <summary>
        /// 解析日志
        /// </summary>
        public List<ParseLog> Logs { get; set; } = new List<ParseLog>();
        
        /// <summary>
        /// 解析结果数据
        /// </summary>
        public ParseResult Result { get; set; }
    }

    /// <summary>
    /// 解析日志模型
    /// </summary>
    public class ParseLog
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 日志类型
        /// </summary>
        public LogType Type { get; set; }
    }

    /// <summary>
    /// 解析结果模型
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// 表头信息
        /// </summary>
        public List<string> Headers { get; set; } = new List<string>();
        
        /// <summary>
        /// 数据行
        /// </summary>
        public List<Dictionary<string, object>> Data { get; set; } = new List<Dictionary<string, object>>();
        
        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalRecords { get; set; }
        
        /// <summary>
        /// 检测到的分隔符
        /// </summary>
        public string Separator { get; set; }
        
        /// <summary>
        /// 检测到的编码格式
        /// </summary>
        public string Encoding { get; set; }
    }

    /// <summary>
    /// Excel 导出请求模型
    /// </summary>
    public class ExcelExportRequest
    {
        /// <summary>
        /// 解析任务ID
        /// </summary>
        [Required]
        public string TaskId { get; set; }
        
        /// <summary>
        /// 导出文件名（可选）
        /// </summary>
        public string FileName { get; set; }
    }

    /// <summary>
    /// 解析状态枚举
    /// </summary>
    public enum ParseStatus
    {
        /// <summary>
        /// 等待中
        /// </summary>
        Pending = 0,
        
        /// <summary>
        /// 解析中
        /// </summary>
        Processing = 1,
        
        /// <summary>
        /// 已完成
        /// </summary>
        Completed = 2,
        
        /// <summary>
        /// 失败
        /// </summary>
        Failed = 3
    }

    /// <summary>
    /// 日志类型枚举
    /// </summary>
    public enum LogType
    {
        /// <summary>
        /// 信息
        /// </summary>
        Info = 0,
        
        /// <summary>
        /// 成功
        /// </summary>
        Success = 1,
        
        /// <summary>
        /// 警告
        /// </summary>
        Warning = 2,
        
        /// <summary>
        /// 错误
        /// </summary>
        Error = 3
    }
}