using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 数据解析服务接口
    /// </summary>
    public interface IDataParserService
    {
        /// <summary>
        /// 开始解析文件
        /// </summary>
        /// <param name="file">上传的文件</param>
        /// <returns>解析任务ID</returns>
        Task<string> StartParseAsync(IFormFile file);
        
        /// <summary>
        /// 获取解析状态
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>解析状态和结果</returns>
        Task<DataParseResponse> GetParseStatusAsync(string taskId);
        
        /// <summary>
        /// 导出为 Excel 文件
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="fileName">文件名</param>
        /// <returns>Excel 文件流</returns>
        Task<(Stream stream, string fileName, string contentType)> ExportToExcelAsync(string taskId, string fileName = null);
        
        /// <summary>
        /// 清理过期的解析任务
        /// </summary>
        /// <returns></returns>
        Task CleanupExpiredTasksAsync();
    }
}