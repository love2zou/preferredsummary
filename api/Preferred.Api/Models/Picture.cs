using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 图片数据实体模型
    /// </summary>
    [Table("Tb_Pictures")]
    public class Picture
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 应用类型
        /// </summary>
        [Required]
        [StringLength(20)]
        public string AppType { get; set; }

        /// <summary>
        /// 图片代码
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ImageCode { get; set; }

        /// <summary>
        /// 图片名称
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ImageName { get; set; }

        /// <summary>
        /// 图片路径
        /// </summary>
        [Required]
        [StringLength(400)]
        public string ImagePath { get; set; }

        /// <summary>
        /// 图片宽高比
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10,4)")]
        public decimal AspectRatio { get; set; }

        /// <summary>
        /// 图片宽度(px)
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// 图片高度(px)
        /// </summary>
        public int? Height { get; set; }

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
    /// 图片创建/更新请求模型
    /// </summary>
    public class PictureDto
    {
        [Required(ErrorMessage = "应用类型不能为空")]
        public string AppType { get; set; }

        [Required(ErrorMessage = "图片代码不能为空")]
        public string ImageCode { get; set; }

        [Required(ErrorMessage = "图片名称不能为空")]
        public string ImageName { get; set; }
        public string ImagePath { get; set; }

        public decimal AspectRatio { get; set; }

        public string AspectRatioString { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public int SeqNo { get; set; }
    
        // 新增：文件扩展名字段
        public string FileExtension { get; set; }
    }

    /// <summary>
    /// 图片列表响应模型
    /// </summary>
    public class PictureListDto
    {
        public int Id { get; set; }
        public string AppType { get; set; }
        public string ImageCode { get; set; }
        public string ImageName { get; set; }
        public string ImagePath { get; set; }
        public decimal AspectRatio { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 图片搜索参数
    /// </summary>
    public class PictureSearchParams
    {
        /// <summary>
        /// 应用类型搜索
        /// </summary>
        public string AppType { get; set; }

        /// <summary>
        /// 图片名称搜索
        /// </summary>
        public string ImageName { get; set; }

        /// <summary>
        /// 图片代码搜索
        /// </summary>
        public string ImageCode { get; set; }
    }

    /// <summary>
    /// 图片上传响应模型
    /// </summary>
    public class UploadResponse
    {
        public string Url { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string AspectRatio { get; set; }
    }
}