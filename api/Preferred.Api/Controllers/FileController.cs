using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 文件管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        
        public FileController(IFileService fileService)
        {
            _fileService = fileService;
        }
        
        /// <summary>
        /// 获取文件列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="size">每页数量</param>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>文件列表</returns>
        [HttpGet]
        public async Task<IActionResult> GetFileList([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] FileSearchParams searchParams = null)
        {
            try
            {
                var files = await _fileService.GetFileList(page, size, searchParams ?? new FileSearchParams());
                var total = await _fileService.GetFileCount(searchParams ?? new FileSearchParams());
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取文件列表成功",
                    Data = new
                    {
                        items = files,
                        total = total,
                        page = page,
                        size = size
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"获取文件列表失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 根据ID获取文件详情
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns>文件详情</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFileById(int id)
        {
            try
            {
                var file = await _fileService.GetFileById(id);
                if (file == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "文件不存在"
                    });
                }
                
                return Ok(new ApiResponse<FileRecord>
                {
                    Success = true,
                    Message = "获取文件详情成功",
                    Data = file
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"获取文件详情失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns>文件流</returns>
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                var (fileStream, fileName, contentType) = await _fileService.GetFileStream(id);
                if (fileStream == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "文件不存在或已被删除"
                    });
                }
                
                return File(fileStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"下载文件失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            try
            {
                var success = await _fileService.DeleteFile(id);
                if (!success)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "文件不存在或删除失败"
                    });
                }
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除文件成功"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"删除文件失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 批量删除文件
        /// </summary>
        /// <param name="ids">文件ID列表</param>
        /// <returns>删除结果</returns>
        [HttpPost("batch-delete")]
        public async Task<IActionResult> BatchDeleteFiles([FromBody] List<int> ids)
        {
            try
            {
                var result = await _fileService.BatchDeleteFiles(ids);
                
                return Ok(new ApiResponse<FileBatchDeleteResult>
                {
                    Success = true,
                    Message = $"批量删除完成，成功：{result.SuccessCount}，失败：{result.FailCount}",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"批量删除失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 清理过期文件
        /// </summary>
        /// <param name="days">过期天数</param>
        /// <returns>清理结果</returns>
        [HttpPost("clean-expired")]
        public async Task<IActionResult> CleanExpiredFiles([FromQuery] int days = 30)
        {
            try
            {
                var result = await _fileService.CleanExpiredFiles(days);
                
                return Ok(new ApiResponse<CleanExpiredFilesResult>
                {
                    Success = true,
                    Message = $"清理完成，删除文件：{result.DeletedCount}，释放空间：{result.FreedSpace / 1024 / 1024:F2}MB",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"清理过期文件失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 上传单个文件
        /// </summary>
        /// <param name="file">上传的文件</param>
        /// <param name="description">文件描述</param>
        /// <param name="appType">应用类型</param>
        /// <returns>上传结果</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(ApiResponse<FileUploadResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string description = "", [FromForm] string appType = "tag")
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "请选择要上传的文件"
                    });
                }
                
                // 验证文件大小 (50MB)
                if (file.Length > 50 * 1024 * 1024)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "文件大小不能超过50MB"
                    });
                }
                
                // 验证文件类型
                var allowedExtensions = new[] { ".xlsx", ".xls", ".csv", ".txt" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "只支持 Excel(.xlsx,.xls)、CSV(.csv)、文本(.txt) 格式的文件"
                    });
                }
                
                // 获取当前用户ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "用户身份验证失败"
                    });
                }
                
                var result = await _fileService.UploadFile(file, description, appType, userId);
                
                return Ok(new ApiResponse<FileUploadResult>
                {
                    Success = true,
                    Message = "文件上传成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"文件上传失败：{ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// 批量上传文件
        /// </summary>
        /// <param name="files">上传的文件列表</param>
        /// <param name="description">文件描述</param>
        /// <param name="appType">应用类型</param>
        /// <returns>批量上传结果</returns>
        [HttpPost("batch-upload")]
        [ProducesResponseType(typeof(ApiResponse<List<FileUploadResult>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BatchUploadFiles(List<IFormFile> files, [FromForm] string description = "", [FromForm] string appType = "tag")
        {
            try
            {
                if (files == null || files.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "请选择要上传的文件"
                    });
                }
                
                if (files.Count > 10)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "最多只能同时上传10个文件"
                    });
                }
                
                // 验证每个文件
                var allowedExtensions = new[] { ".xlsx", ".xls", ".csv", ".txt" };
                foreach (var file in files)
                {
                    if (file.Length > 50 * 1024 * 1024)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = $"文件 {file.FileName} 大小不能超过50MB"
                        });
                    }
                    
                    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = $"文件 {file.FileName} 格式不支持，只支持 Excel、CSV、文本格式"
                        });
                    }
                }
                
                // 获取当前用户ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "用户身份验证失败"
                    });
                }
                
                var results = await _fileService.BatchUploadFiles(files, description, appType, userId);
                
                return Ok(new ApiResponse<List<FileUploadResult>>
                {
                    Success = true,
                    Message = $"批量上传完成，成功上传 {results.Count} 个文件",
                    Data = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"批量上传失败：{ex.Message}"
                });
            }
        }
    }
}