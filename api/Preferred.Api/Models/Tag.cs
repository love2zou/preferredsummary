using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 标签实体模型
    /// </summary>
    [Table("Tb_Tag")]
    public class Tag
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 应用模块
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ParName { get; set; }

        /// <summary>
        /// 标签代码
        /// </summary>
        [Required]
        [StringLength(20)]
        public string TagCode { get; set; }

        /// <summary>
        /// 标签名称
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TagName { get; set; }

        /// <summary>
        /// 标签字体颜色(如：#EF0011)
        /// </summary>
        [Required]
        [StringLength(10)]
        public string HexColor { get; set; }

        /// <summary>
        /// 标签背景色(如：rgb(1,32,12,0.1))
        /// </summary>
        [Required]
        [StringLength(50)]
        public string RgbColor { get; set; }

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
    /// 标签创建/更新请求模型
    /// </summary>
    public class TagDto
    {
        /// <summary>
        /// 应用模块
        /// </summary>
        /// <example>UserManagement</example>
        [Required(ErrorMessage = "应用模块不能为空")]
        [StringLength(50, ErrorMessage = "应用模块长度不能超过50个字符")]
        public string ParName { get; set; }

        /// <summary>
        /// 标签代码
        /// </summary>
        /// <example>ACTIVE</example>
        [Required(ErrorMessage = "标签代码不能为空")]
        [StringLength(20, ErrorMessage = "标签代码长度不能超过20个字符")]
        public string TagCode { get; set; }

        /// <summary>
        /// 标签名称
        /// </summary>
        /// <example>活跃用户</example>
        [Required(ErrorMessage = "标签名称不能为空")]
        [StringLength(50, ErrorMessage = "标签名称长度不能超过50个字符")]
        public string TagName { get; set; }

        /// <summary>
        /// 标签字体颜色
        /// </summary>
        /// <example>#FFFFFF</example>
        [Required(ErrorMessage = "字体颜色不能为空")]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "字体颜色格式不正确，应为#RRGGBB格式")]
        public string HexColor { get; set; }

        /// <summary>
        /// 标签背景色
        /// </summary>
        /// <example>rgba(239, 0, 17, 0.1)</example>
        [Required(ErrorMessage = "背景色不能为空")]
        [StringLength(50, ErrorMessage = "背景色长度不能超过50个字符")]
        public string RgbColor { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        /// <example>1</example>
        public int SeqNo { get; set; } = 0;
    }

    /// <summary>
    /// 标签列表响应模型
    /// </summary>
    public class TagListDto
    {
        public int Id { get; set; }
        public string ParName { get; set; }
        public string TagCode { get; set; }
        public string TagName { get; set; }
        public string HexColor { get; set; }
        public string RgbColor { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 标签搜索参数
    /// </summary>
    public class TagSearchParams
    {
        /// <summary>
        /// 应用模块搜索
        /// </summary>
        public string ParName { get; set; }

        /// <summary>
        /// 标签代码搜索
        /// </summary>
        public string TagCode { get; set; }

        /// <summary>
        /// 标签名称搜索
        /// </summary>
        public string TagName { get; set; }
    }

    /// <summary>
    /// 标签导入结果模型
    /// </summary>
    public class TagImportResult
    {
        /// <summary>
        /// 行号
        /// </summary>
        public int RowNumber { get; set; }
        
        /// <summary>
        /// 应用模块
        /// </summary>
        public string ParName { get; set; }
        
        /// <summary>
        /// 标签代码
        /// </summary>
        public string TagCode { get; set; }
        
        /// <summary>
        /// 标签名称
        /// </summary>
        public string TagName { get; set; }
        
        /// <summary>
        /// 排序号
        /// </summary>
        public int SeqNo { get; set; }
        
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 提示信息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 生成的字体颜色
        /// </summary>
        public string HexColor { get; set; }
        
        /// <summary>
        /// 生成的背景颜色
        /// </summary>
        public string RgbColor { get; set; }
    }
    
    /// <summary>
    /// 标签导入进度模型
    /// </summary>
    public class TagImportProgress
    {
        /// <summary>
        /// 当前处理行数
        /// </summary>
        public int CurrentRow { get; set; }
        
        /// <summary>
        /// 总行数
        /// </summary>
        public int TotalRows { get; set; }
        
        /// <summary>
        /// 进度百分比
        /// </summary>
        public int Percentage { get; set; }
        
        /// <summary>
        /// 当前状态
        /// </summary>
        public string Status { get; set; }
    }
}