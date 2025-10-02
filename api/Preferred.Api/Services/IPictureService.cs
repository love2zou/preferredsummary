using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 图片数据服务接口
    /// </summary>
    public interface IPictureService
    {
        /// <summary>
        /// 获取图片列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="size">每页数量</param>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>图片列表</returns>
        Task<List<PictureListDto>> GetPictureList(int page, int size, PictureSearchParams searchParams);

        /// <summary>
        /// 获取图片总数
        /// </summary>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>总数</returns>
        Task<int> GetPictureCount(PictureSearchParams searchParams);

        /// <summary>
        /// 根据应用类型获取图片列表
        /// </summary>
        /// <param name="appType">应用类型</param>
        /// <returns>图片列表</returns>
        Task<List<PictureListDto>> GetPicturesByAppType(string appType);

        /// <summary>
        /// 根据ID获取图片详情
        /// </summary>
        /// <param name="id">图片ID</param>
        /// <returns>图片详情</returns>
        Task<Picture> GetPictureById(int id);

        /// <summary>
        /// 创建图片
        /// </summary>
        /// <param name="pictureDto">图片信息</param>
        /// <returns>是否成功</returns>
        Task<bool> CreatePicture(PictureDto pictureDto);

        /// <summary>
        /// 更新图片
        /// </summary>
        /// <param name="id">图片ID</param>
        /// <param name="pictureDto">图片信息</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdatePicture(int id, PictureDto pictureDto);

        /// <summary>
        /// 删除图片
        /// </summary>
        /// <param name="id">图片ID</param>
        /// <returns>是否成功</returns>
        Task<bool> DeletePicture(int id);

        /// <summary>
        /// 删除图片文件
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>是否成功</returns>
        Task<bool> DeleteImageFile(string imagePath);

        /// <summary>
        /// 检查图片代码是否存在
        /// </summary>
        /// <param name="imageCode">图片代码</param>
        /// <param name="excludeId">排除的ID</param>
        /// <returns>是否存在</returns>
        Task<bool> IsImageCodeExists(string imageCode, int? excludeId = null);
    }
}