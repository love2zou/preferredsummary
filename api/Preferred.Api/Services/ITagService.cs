using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;
using System.IO;
using System;

namespace Preferred.Api.Services
{
    public interface ITagService
    {
        /// <summary>
        /// 获取标签列表
        /// </summary>
        Task<List<TagListDto>> GetTagList(int page = 1, int pageSize = 10, TagSearchParams searchParams = null);
        
        /// <summary>
        /// 获取标签总数
        /// </summary>
        Task<int> GetTagCount(TagSearchParams searchParams = null);
        
        /// <summary>
        /// 根据ID获取标签
        /// </summary>
        Task<Tag> GetTagById(int id);
        
        /// <summary>
        /// 根据应用模块获取标签列表
        /// </summary>
        Task<List<string>> GetParNameList();
        
        /// <summary>
        /// 根据应用模块名称获取标签列表
        /// </summary>
        Task<List<TagListDto>> GetTagsByParName(string parName);
        
        /// <summary>
        /// 创建标签
        /// </summary>
        Task<bool> CreateTag(TagDto tagDto);
        
        /// <summary>
        /// 更新标签
        /// </summary>
        Task<bool> UpdateTag(int id, TagDto tagDto);
        
        /// <summary>
        /// 删除标签
        /// </summary>
        Task<bool> DeleteTag(int id);
        
        /// <summary>
        /// 检查标签代码是否存在
        /// </summary>
        Task<bool> IsTagCodeExists(string parName, string tagCode, int? excludeId = null);
        
        /// <summary>
        /// 导入标签数据
        /// </summary>
        Task<List<TagImportResult>> ImportTagsFromExcel(Stream excelStream, IProgress<TagImportProgress> progress = null);

        /// <summary>
        /// 生成导入结果Excel文件
        /// </summary>
        /// <param name="results">导入结果列表</param>
        /// <returns>Excel文件字节数组</returns>
        byte[] GenerateImportResultExcel(List<TagImportResult> results);
        
        /// <summary>
        /// 批量删除标签
        /// </summary>
        /// <param name="ids">标签ID数组</param>
        /// <returns>删除结果</returns>
        Task<BatchDeleteResult> BatchDeleteTags(int[] ids);
    }
    
    /// <summary>
    /// 批量删除结果
    /// </summary>
    public class BatchDeleteResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<int> FailedIds { get; set; } = new List<int>();
    }
}