using System.Collections.Generic;

namespace Preferred.Api.Models
{
    /// <summary>
    /// API响应基础模型
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 响应消息
        /// </summary>
        /// <example>操作成功</example>
        public string Message { get; set; }
        
        /// <summary>
        /// 响应数据
        /// </summary>
        public T Data { get; set; }
        
        /// <summary>
        /// 错误代码（可选）
        /// </summary>
        public string? ErrorCode { get; set; }
}

/// <summary>
/// 用户名和姓名对
/// </summary>
public class UserNamePair
{
    /// <summary>
    /// 英文用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>
    /// 中文姓名
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    }
    
    /// <summary>
    /// 无数据的 API 响应
    /// </summary>
    public class ApiResponse : ApiResponse<object>
    {
    }

    /// <summary>
    /// API错误响应模型
    /// </summary>
    public class ApiErrorResponse
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        /// <example>用户名或密码错误</example>
        public string Message { get; set; }

        /// <summary>
        /// 错误详情（可选）
        /// </summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// 分页响应模型
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class PagedResponse<T>
    {
        /// <summary>
        /// 数据列表
        /// </summary>
        public List<T> Data { get; set; }
        
        /// <summary>
        /// 总记录数
        /// </summary>
        public int Total { get; set; }
        
        /// <summary>
        /// 当前页码
        /// </summary>
        public int Page { get; set; }
        
        /// <summary>
        /// 每页大小
        /// </summary>
        public int PageSize { get; set; }
        
        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }
    }
}