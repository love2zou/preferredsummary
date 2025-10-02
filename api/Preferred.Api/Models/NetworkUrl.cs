using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 访问地址实体模型
    /// </summary>
    [Table("Tb_NetWorkURL")]
    public class NetworkUrl
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 域名地址
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 名称
        /// </summary>
        [Required]
        [StringLength(100)]
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        [StringLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// 图片代码
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ImageCode { get; set; } = string.Empty;

        /// <summary>
        /// 分类代码
        /// </summary>
        [StringLength(50)]
        public string? CategoryCode { get; set; }

        /// <summary>
        /// 是否可用
        /// </summary>
        public long? IsAvailable { get; set; }

        /// <summary>
        /// 是否推荐
        /// </summary>
        [Required]
        public long IsMark { get; set; }

        /// <summary>
        /// 标签类型
        /// </summary>
        [Required]
        [StringLength(200)]
        public string TagCodeType { get; set; } = string.Empty;

        /// <summary>
        /// 排序号
        /// </summary>
        [Required]
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
    /// 访问地址DTO
    /// </summary>
    public class NetworkUrlDto
    {
        /// <summary>
        /// 域名地址
        /// </summary>
        [Required(ErrorMessage = "域名地址不能为空")]
        [StringLength(100, ErrorMessage = "域名地址长度不能超过100个字符")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength(100, ErrorMessage = "名称长度不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        [StringLength(200, ErrorMessage = "描述长度不能超过200个字符")]
        public string? Description { get; set; }

        /// <summary>
        /// 图片代码
        /// </summary>
        [Required(ErrorMessage = "图片代码不能为空")]
        [StringLength(50, ErrorMessage = "图片代码长度不能超过50个字符")]
        public string ImageCode { get; set; } = string.Empty;

        /// <summary>
        /// 分类代码
        /// </summary>
        [StringLength(50, ErrorMessage = "分类代码长度不能超过50个字符")]
        public string? CategoryCode { get; set; }

        /// <summary>
        /// 是否可用
        /// </summary>
        public long? IsAvailable { get; set; }

        /// <summary>
        /// 是否推荐
        /// </summary>
        [Required(ErrorMessage = "是否推荐不能为空")]
        public long IsMark { get; set; }

        /// <summary>
        /// 标签类型
        /// </summary>
        [Required(ErrorMessage = "标签类型不能为空")]
        [StringLength(200, ErrorMessage = "标签类型长度不能超过200个字符")]
        public string TagCodeType { get; set; } = string.Empty;

        /// <summary>
        /// 排序号
        /// </summary>
        [Required(ErrorMessage = "排序号不能为空")]
        public int SeqNo { get; set; } = 0;
    }

    /// <summary>
    /// 访问地址列表DTO
    /// </summary>
    public class NetworkUrlListDto
    {
        public int Id { get; set; }
        public string? Url { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageCode { get; set; }
        public string? CategoryCode { get; set; }
        public long? IsAvailable { get; set; }  // 改为 long?
        public long IsMark { get; set; }        // 改为 long
        public string? TagCodeType { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
        public string? PictureUrl { get; set; }
        public string? PictureName { get; set; }

        // 新增分类字段
        public string? CategoryName { get; set; }
        public string? CategoryIcon { get; set; }
        public string? CategoryDescription { get; set; }

        // 新增多标签字段
        public List<TagInfo>? Tags { get; set; }
    }

    public class TagInfo
    {
        public string CodeType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string HexColor { get; set; } = string.Empty;
        public string RgbColor { get; set; } = string.Empty;
    }
    /// <summary>
    /// 访问地址搜索参数
    /// </summary>
    public class NetworkUrlSearchParams
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 标签类型列表（多选）
        /// </summary>
        public List<string>? TagCodeTypes { get; set; }

        /// <summary>
        /// 分类代码
        /// </summary>
        public string? CategoryCode { get; set; }

        /// <summary>
        /// 是否推荐
        /// </summary>
        public long? IsMark { get; set; }

        /// <summary>
        /// 是否可用
        /// </summary>
        public long? IsAvailable { get; set; }
    }
}