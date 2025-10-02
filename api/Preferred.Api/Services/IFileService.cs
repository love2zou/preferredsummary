using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 文件服务接口
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// 获取文件列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="size">每页数量</param>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>文件列表</returns>
        Task<List<FileListDto>> GetFileList(int page, int size, FileSearchParams searchParams);
        
        /// <summary>
        /// 获取文件总数
        /// </summary>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>文件总数</returns>
        Task<int> GetFileCount(FileSearchParams searchParams);
        
        /// <summary>
        /// 根据ID获取文件详情
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns>文件详情</returns>
        Task<FileRecord> GetFileById(int id);
        
        /// <summary>
        /// 保存文件记录
        /// </summary>
        /// <param name="fileRecord">文件记录</param>
        /// <returns>保存结果</returns>
        Task<bool> SaveFileRecord(FileRecord fileRecord);
        
        /// <summary>
        /// 获取文件流用于下载
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns>文件流和文件信息</returns>
        Task<(Stream fileStream, string fileName, string contentType)> GetFileStream(int id);
        
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns>删除结果</returns>
        Task<bool> DeleteFile(int id);
        
        /// <summary>
        /// 批量删除文件
        /// </summary>
        /// <param name="ids">文件ID列表</param>
        /// <returns>删除结果</returns>
        Task<FileBatchDeleteResult> BatchDeleteFiles(List<int> ids);
        
        /// <summary>
        /// 清理过期文件
        /// </summary>
        /// <param name="days">过期天数</param>
        /// <returns>清理结果</returns>
        Task<CleanExpiredFilesResult> CleanExpiredFiles(int days);
        
        /// <summary>
        /// 上传单个文件
        /// </summary>
        /// <param name="file">上传的文件</param>
        /// <param name="description">文件描述</param>
        /// <param name="appType">应用类型</param>
        /// <param name="userId">用户ID</param>
        /// <returns>上传结果</returns>
        Task<FileUploadResult> UploadFile(IFormFile file, string description, string appType, int userId);
        
        /// <summary>
        /// 批量上传文件
        /// </summary>
        /// <param name="files">上传的文件列表</param>
        /// <param name="description">文件描述</param>
        /// <param name="appType">应用类型</param>
        /// <param name="userId">用户ID</param>
        /// <returns>批量上传结果</returns>
        Task<List<FileUploadResult>> BatchUploadFiles(List<IFormFile> files, string description, string appType, int userId);
    }
}