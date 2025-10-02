using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 文件记录实体模型
    /// </summary>
    [Table("Tb_FileRecord")]
    public class FileRecord
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// 文件名称
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件类型
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string FileType { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件路径
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件大小(Mb)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FileSize { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件描述
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; } // 移除 [Required] 并改为可空类型
        
        /// <summary>
        /// 用户ID
        /// </summary>
        public int UploadUserId { get; set; } = 0;
        
        /// <summary>
        /// 应用类型
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string AppType { get; set; } = string.Empty;
        
        /// <summary>
        /// 排序号
        /// </summary>
        public int SeqNo { get; set; } = 0;
        
        /// <summary>
        /// 创建时间
        /// </summary>
        [Required]
        public DateTime CrtTime { get; set; }
        
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Required]
        public DateTime UpdTime { get; set; }
    }
    
    /// <summary>
    /// 文件搜索参数
    /// </summary>
    public class FileSearchParams
    {
        /// <summary>
        /// 文件名搜索
        /// </summary>
        public string? FileName { get; set; }
        
        /// <summary>
        /// 文件类型搜索
        /// </summary>
        public string? FileType { get; set; }
        
        /// <summary>
        /// 上传用户搜索
        /// </summary>
        public string? UploadUser { get; set; }
        
        /// <summary>
        /// 应用类型搜索
        /// </summary>
        public string? AppType { get; set; }
        
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartTime { get; set; }
        
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }
    }
    
    /// <summary>
    /// 文件列表响应模型
    /// </summary>
    public class FileListDto
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FileSize { get; set; }
        public DateTime CrtTime { get; set; }
        public string? Description { get; set; }
        public int UploadUserId { get; set; }  // 显示用户ID
        public string? UploadUserName { get; set; }  // 添加用户名字段
        public string AppType { get; set; }  // 添加应用类型字段
    }
    
    /// <summary>
    /// 批量删除结果
    /// </summary>
    public class FileBatchDeleteResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<int> FailedIds { get; set; } = new List<int>();
    }
    
    /// <summary>
    /// 清理过期文件结果
    /// </summary>
    public class CleanExpiredFilesResult
    {
        public int DeletedCount { get; set; }
        public long FreedSpace { get; set; }
    }
}