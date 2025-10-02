using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 分类实体模型
    /// </summary>
    [Table("Tb_CategoryMenu")]
    public class Category
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 分类代码
        /// </summary>
        [Required]
        [StringLength(50)]
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; }

        /// <summary>
        /// 分类图标
        /// </summary>
        [StringLength(50)]
        public string CategoryIcon { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        [StringLength(500)]
        public string Description { get; set; }

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
    /// 分类创建/更新请求模型
    /// </summary>
    public class CategoryDto
    {
        /// <summary>
        /// 分类代码
        /// </summary>
        /// <example>DESIGN</example>
        [Required(ErrorMessage = "分类代码不能为空")]
        [StringLength(50, ErrorMessage = "分类代码长度不能超过50个字符")]
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        /// <example>设计工具</example>
        [Required(ErrorMessage = "分类名称不能为空")]
        [StringLength(100, ErrorMessage = "分类名称长度不能超过100个字符")]
        public string CategoryName { get; set; }

        /// <summary>
        /// 分类图标
        /// </summary>
        /// <example>el-icon-design</example>
        [StringLength(50, ErrorMessage = "分类图标长度不能超过50个字符")]
        public string CategoryIcon { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        /// <example>设计相关的工具和资源</example>
        [StringLength(500, ErrorMessage = "描述长度不能超过500个字符")]
        public string Description { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        /// <example>1</example>
        public int SeqNo { get; set; } = 0;
    }

    /// <summary>
    /// 分类列表响应模型
    /// </summary>
    public class CategoryListDto
    {
        public int Id { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public string CategoryIcon { get; set; }
        public string Description { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 分类搜索参数
    /// </summary>
    public class CategorySearchParams
    {
        /// <summary>
        /// 分类代码搜索
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称搜索
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 描述搜索
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 批量删除请求模型
    /// </summary>
    public class BatchDeleteRequest
    {
        /// <summary>
        /// 要删除的ID列表
        /// </summary>
        [Required(ErrorMessage = "ID列表不能为空")]
        public int[] Ids { get; set; }
    }
}