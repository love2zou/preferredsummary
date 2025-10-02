using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 通知实体模型
    /// </summary>
    [Table("Tb_Notification")]
    public class Notification
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 是否已读(0 未读, 1 已读)
        /// </summary>
        public int IsRead { get; set; } = 0;

        /// <summary>
        /// 标题
        /// </summary>
        [Required]
        [StringLength(36)]
        public string Name { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        [Required]
        [StringLength(400)]
        public string Content { get; set; }

        /// <summary>
        /// 通知类型(接口)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string NotifyType { get; set; }

        /// <summary>
        /// 通知状态(0 正常, 1 错误, 2 告警)
        /// </summary>
        public int NotifyStatus { get; set; } = 0;

        /// <summary>
        /// 发送时间
        /// </summary>
        [Required]
        public DateTime SendTime { get; set; }

        /// <summary>
        /// 发送人
        /// </summary>
        [Required]
        [StringLength(100)]
        public string SendUser { get; set; }

        /// <summary>
        /// 接收人
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Receiver { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(200)]
        public string Remark { get; set; }

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
    /// 通知列表DTO
    /// </summary>
    public class NotificationListDto
    {
        public int Id { get; set; }
        public int IsRead { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public string NotifyType { get; set; }
        public int NotifyStatus { get; set; }
        public DateTime SendTime { get; set; }
        public string SendUser { get; set; }
        public string Receiver { get; set; }
        public string Remark { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 通知创建DTO
    /// </summary>
    public class NotificationCreateDto
    {
        [Required(ErrorMessage = "标题不能为空")]
        [StringLength(36, ErrorMessage = "标题长度不能超过36个字符")]
        public string Name { get; set; }

        [Required(ErrorMessage = "内容不能为空")]
        [StringLength(400, ErrorMessage = "内容长度不能超过400个字符")]
        public string Content { get; set; }

        [Required(ErrorMessage = "通知类型不能为空")]
        [StringLength(20, ErrorMessage = "通知类型长度不能超过20个字符")]
        public string NotifyType { get; set; }

        public int NotifyStatus { get; set; } = 0;

        [Required(ErrorMessage = "发送时间不能为空")]
        public DateTime SendTime { get; set; }

        [Required(ErrorMessage = "发送人不能为空")]
        [StringLength(100, ErrorMessage = "发送人长度不能超过100个字符")]
        public string SendUser { get; set; }

        [Required(ErrorMessage = "接收人不能为空")]
        [StringLength(100, ErrorMessage = "接收人长度不能超过100个字符")]
        public string Receiver { get; set; }

        [StringLength(200, ErrorMessage = "备注长度不能超过200个字符")]
        public string Remark { get; set; }

        public int SeqNo { get; set; } = 0;
    }

    /// <summary>
    /// 通知更新DTO
    /// </summary>
    public class NotificationUpdateDto
    {
        [Required(ErrorMessage = "ID不能为空")]
        public int Id { get; set; }

        public int? IsRead { get; set; }

        [StringLength(36, ErrorMessage = "标题长度不能超过36个字符")]
        public string Name { get; set; }

        [StringLength(400, ErrorMessage = "内容长度不能超过400个字符")]
        public string Content { get; set; }

        [StringLength(20, ErrorMessage = "通知类型长度不能超过20个字符")]
        public string NotifyType { get; set; }

        public int? NotifyStatus { get; set; }

        public DateTime? SendTime { get; set; }

        [StringLength(100, ErrorMessage = "发送人长度不能超过100个字符")]
        public string SendUser { get; set; }

        [StringLength(100, ErrorMessage = "接收人长度不能超过100个字符")]
        public string Receiver { get; set; }

        [StringLength(200, ErrorMessage = "备注长度不能超过200个字符")]
        public string Remark { get; set; }

        public int? SeqNo { get; set; }
    }

    /// <summary>
    /// 通知搜索参数
    /// </summary>
    public class NotificationSearchParams
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public string NotifyType { get; set; }
        public int? NotifyStatus { get; set; }
        public int? IsRead { get; set; }
        public string SendUser { get; set; }
        public string Receiver { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}