using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 文件服务实现
    /// </summary>
    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _uploadPath;
        
        public FileService(ApplicationDbContext context)
        {
            _context = context;
            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "upload","fileRecv");
            
            // 确保上传目录存在
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }
        
        public async Task<List<FileListDto>> GetFileList(int page, int size, FileSearchParams searchParams)
        {
            var query = _context.FileRecords.AsQueryable();
            
            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.FileName))
                {
                    query = query.Where(f => f.FileName.Contains(searchParams.FileName));
                }
                
                if (!string.IsNullOrEmpty(searchParams.FileType))
                {
                    query = query.Where(f => f.FileType == searchParams.FileType);
                }
                
                if (!string.IsNullOrEmpty(searchParams.AppType))
                {
                    query = query.Where(f => f.AppType == searchParams.AppType);
                }
                
                // 添加上传用户搜索条件
                if (!string.IsNullOrEmpty(searchParams.UploadUser))
                {
                    // 通过用户名搜索，需要 Join Users 表
                    var userQuery = _context.Users.Where(u => u.UserName.Contains(searchParams.UploadUser)).Select(u => u.Id);
                    query = query.Where(f => userQuery.Contains(f.UploadUserId));
                }
                
                if (searchParams.StartTime.HasValue)
                {
                    query = query.Where(f => f.CrtTime >= searchParams.StartTime.Value);
                }
                
                if (searchParams.EndTime.HasValue)
                {
                    query = query.Where(f => f.CrtTime <= searchParams.EndTime.Value);
                }
            }
            
            return await query
                .OrderByDescending(f => f.CrtTime)
                .Skip((page - 1) * size)
                .Take(size)
                .Join(_context.Users,
                      f => f.UploadUserId,
                      u => u.Id,
                      (f, u) => new { File = f, User = u })
                .Select(joined => new FileListDto
                {
                    Id = joined.File.Id,
                    FileName = joined.File.FileName,
                    FileType = joined.File.FileType,
                    FileSize = joined.File.FileSize,
                    CrtTime = joined.File.CrtTime,
                    Description = joined.File.Description,
                    UploadUserId = joined.File.UploadUserId,
                    UploadUserName = joined.User.UserName,
                    AppType = joined.File.AppType  // 添加应用类型
                })
                .ToListAsync();
        }
        
        public async Task<int> GetFileCount(FileSearchParams searchParams)
        {
            var query = _context.FileRecords.AsQueryable();
            
            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.FileName))
                {
                    query = query.Where(f => f.FileName.Contains(searchParams.FileName));
                }
                
                if (!string.IsNullOrEmpty(searchParams.FileType))
                {
                    query = query.Where(f => f.FileType == searchParams.FileType);
                }
                
                if (!string.IsNullOrEmpty(searchParams.AppType))
                {
                    query = query.Where(f => f.AppType == searchParams.AppType);
                }
                
                // 添加上传用户搜索条件
                if (!string.IsNullOrEmpty(searchParams.UploadUser))
                {
                    var userQuery = _context.Users.Where(u => u.UserName.Contains(searchParams.UploadUser)).Select(u => u.Id);
                    query = query.Where(f => userQuery.Contains(f.UploadUserId));
                }
                
                if (searchParams.StartTime.HasValue)
                {
                    query = query.Where(f => f.CrtTime >= searchParams.StartTime.Value);
                }
                
                if (searchParams.EndTime.HasValue)
                {
                    query = query.Where(f => f.CrtTime <= searchParams.EndTime.Value);
                }
            }
            
            return await query.CountAsync();
        }
        
        public async Task<FileRecord> GetFileById(int id)
        {
            return await _context.FileRecords.FindAsync(id);
        }
        
        public async Task<bool> SaveFileRecord(FileRecord fileRecord)
        {
            try
            {
                if (fileRecord.Id == 0)
                {
                    fileRecord.CrtTime = DateTime.Now;
                    fileRecord.UpdTime = DateTime.Now;
                    _context.FileRecords.Add(fileRecord);
                }
                else
                {
                    fileRecord.UpdTime = DateTime.Now;
                    _context.FileRecords.Update(fileRecord);
                }
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<(Stream fileStream, string fileName, string contentType)> GetFileStream(int id)
        {
            var fileRecord = await _context.FileRecords.FindAsync(id);
            if (fileRecord == null)
            {
                return (null, null, null);
            }
            
            var filePath = Path.Combine(_uploadPath, fileRecord.FilePath);
            if (!File.Exists(filePath))
            {
                return (null, null, null);
            }
            
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var contentType = GetContentType(fileRecord.FileType);
            
            return (fileStream, fileRecord.FileName, contentType);
        }
        
        public async Task<bool> DeleteFile(int id)
        {
            try
            {
                var fileRecord = await _context.FileRecords.FindAsync(id);
                if (fileRecord == null)
                {
                    return false;
                }
                
                // 删除物理文件
                var filePath = Path.Combine(_uploadPath, fileRecord.FilePath);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                // 删除数据库记录
                _context.FileRecords.Remove(fileRecord);
                await _context.SaveChangesAsync();
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<FileBatchDeleteResult> BatchDeleteFiles(List<int> ids)
        {
            var result = new FileBatchDeleteResult();
            
            foreach (var id in ids)
            {
                var success = await DeleteFile(id);
                if (success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailCount++;
                    result.FailedIds.Add(id);
                }
            }
            
            return result;
        }
        
        public async Task<CleanExpiredFilesResult> CleanExpiredFiles(int days)
        {
            var result = new CleanExpiredFilesResult();
            var expiredDate = DateTime.Now.AddDays(-days);
            
            var expiredFiles = await _context.FileRecords
                .Where(f => f.CrtTime < expiredDate)
                .ToListAsync();
            
            foreach (var file in expiredFiles)
            {
                var filePath = Path.Combine(_uploadPath, file.FilePath);
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    result.FreedSpace += fileInfo.Length;
                    File.Delete(filePath);
                }
                
                _context.FileRecords.Remove(file);
                result.DeletedCount++;
            }
            
            await _context.SaveChangesAsync();
            return result;
        }
        
        private string GetContentType(string fileType)
        {
            return fileType.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
        
        public async Task<FileUploadResult> UploadFile(IFormFile file, string description, string appType, int userId)
        {
            try
            {
                // 验证文件
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("文件不能为空");
                }

                // 生成唯一文件名
                var fileExtension = Path.GetExtension(file.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(_uploadPath, uniqueFileName);

                // 保存文件到磁盘
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 创建文件记录
                var fileRecord = new FileRecord
                {
                    FileName = file.FileName,
                    FilePath = uniqueFileName,
                    FileType = fileExtension,
                    FileSize = FormatFileSize(file.Length),
                    Description = description ?? string.Empty,
                    AppType = appType ?? "default",
                    UploadUserId = userId,  // 添加这一行
                    CrtTime = DateTime.Now,
                    UpdTime = DateTime.Now
                };

                // 保存到数据库
                _context.FileRecords.Add(fileRecord);
                await _context.SaveChangesAsync();

                return new FileUploadResult
                {
                    Id = fileRecord.Id,
                    FileName = fileRecord.FileName,
                    FileSize = file.Length,
                    FilePath = uniqueFileName,
                    Success = true,
                    UploadTime = DateTime.Now,
                    Message = "文件上传成功"
                };
            }
            catch (Exception ex)
            {
                return new FileUploadResult
                {
                    Success = false,
                    Message = $"文件上传失败：{ex.Message}"
                };
            }
        }

        public async Task<List<FileUploadResult>> BatchUploadFiles(List<IFormFile> files, string description, string appType, int userId)
        {
            var results = new List<FileUploadResult>();

            foreach (var file in files)
            {
                var result = await UploadFile(file, description, appType, userId);
                results.Add(result);
            }

            return results;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}