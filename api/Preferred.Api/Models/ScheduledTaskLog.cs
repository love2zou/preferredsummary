using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 定时任务执行日志实体模型
    /// </summary>
    [Table("Tb_ScheduledTaskLog")]
    public class ScheduledTaskLog
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        [Required]
        public int TaskId { get; set; }

        /// <summary>
        /// 任务编码（冗余记录）
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>
        /// 开始时间
        /// </summary>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        [Required]
        public bool Success { get; set; } = false;

        /// <summary>
        /// 执行结果或异常信息
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string? Message { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Required]
        public DateTime CrtTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Required]
        public DateTime UpdTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 关联的定时任务
        /// </summary>
        [ForeignKey("TaskId")]
        public virtual ScheduledTask? ScheduledTask { get; set; }
    }
}