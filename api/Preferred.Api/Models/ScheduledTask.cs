using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 定时任务实体模型
    /// </summary>
    [Table("Tb_ScheduledTask")]
    public class ScheduledTask
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 任务名称
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 任务编码（唯一标识）
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Cron 表达式
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Cron { get; set; } = string.Empty;

        /// <summary>
        /// 执行处理器（类名或URL或脚本路径）
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Handler { get; set; } = string.Empty;

        /// <summary>
        /// 执行参数（可选，JSON 格式）
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string? Parameters { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        [Required]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 最后运行时间
        /// </summary>
        public DateTime? LastRunTime { get; set; }

        /// <summary>
        /// 下次运行时间
        /// </summary>
        public DateTime? NextRuntime { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [MaxLength(255)]
        public string? Remark { get; set; }

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
    }
}