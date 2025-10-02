using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 文件上传结果
    /// </summary>
    public class FileUploadResult
    {
        /// <summary>
        /// 文件ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 上传时间
        /// </summary>
        public DateTime UploadTime { get; set; }
        
        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }
    }
}