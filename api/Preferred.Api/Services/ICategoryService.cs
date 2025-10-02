using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 分类服务接口
    /// </summary>
    public interface ICategoryService
    {
        /// <summary>
        /// 获取分类列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页数量</param>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>分类列表</returns>
        Task<List<CategoryListDto>> GetCategoryList(int page = 1, int pageSize = 10, CategorySearchParams searchParams = null);
        
        /// <summary>
        /// 获取分类总数
        /// </summary>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>总数</returns>
        Task<int> GetCategoryCount(CategorySearchParams searchParams = null);
        
        /// <summary>
        /// 根据ID获取分类
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <returns>分类详情</returns>
        Task<Category> GetCategoryById(int id);
        
        /// <summary>
        /// 获取所有分类代码列表
        /// </summary>
        /// <returns>分类代码列表</returns>
        Task<List<string>> GetCategoryCodeList();
        
        /// <summary>
        /// 创建分类
        /// </summary>
        /// <param name="categoryDto">分类创建信息</param>
        /// <returns>是否成功</returns>
        Task<bool> CreateCategory(CategoryDto categoryDto);
        
        /// <summary>
        /// 更新分类
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <param name="categoryDto">分类更新信息</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateCategory(int id, CategoryDto categoryDto);
        
        /// <summary>
        /// 删除分类
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <returns>是否成功</returns>
        Task<bool> DeleteCategory(int id);
        
        /// <summary>
        /// 检查分类代码是否存在
        /// </summary>
        /// <param name="categoryCode">分类代码</param>
        /// <param name="excludeId">排除的ID（用于更新时检查）</param>
        /// <returns>是否存在</returns>
        Task<bool> IsCategoryCodeExists(string categoryCode, int? excludeId = null);
        
        /// <summary>
        /// 批量删除分类
        /// </summary>
        /// <param name="ids">分类ID数组</param>
        /// <returns>删除结果</returns>
        Task<BatchDeleteResult> BatchDeleteCategories(int[] ids);
        
        /// <summary>
        /// 更新分类排序
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <param name="seqNo">新的排序号</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateCategorySeqNo(int id, int seqNo);
    }
}