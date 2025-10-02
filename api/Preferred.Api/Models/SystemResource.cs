using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 系统资源占用实体模型
    /// </summary>
    [Table("Tb_SystemResource")]
    public class SystemResource
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 主机名称
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string HostName { get; set; } = string.Empty;

        /// <summary>
        /// CPU占用百分比，保留两位小数
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal CpuUsage { get; set; }

        /// <summary>
        /// 内存占用百分比，保留两位小数
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MemoryUsage { get; set; }

        /// <summary>
        /// 系统盘名称
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string DiskName { get; set; } = string.Empty;

        /// <summary>
        /// 磁盘占用百分比，保留两位小数
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiskUsage { get; set; }

        /// <summary>
        /// 总磁盘容量，单位GB
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal DiskTotal { get; set; }

        /// <summary>
        /// 已使用磁盘容量，单位GB
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal DiskUsed { get; set; }

        /// <summary>
        /// 可用磁盘容量，单位GB
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal DiskFree { get; set; }

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