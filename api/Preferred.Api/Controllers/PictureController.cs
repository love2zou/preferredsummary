using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Preferred.Api.Models;
using Preferred.Api.Services;
using SixLabors.ImageSharp;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Preferred.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class PictureController : ControllerBase {
        private readonly IPictureService _pictureService;
        private readonly FileStorageConfig _fileStorageConfig;
        private readonly string _uploadPath;

        public PictureController(IPictureService pictureService, IOptions<FileStorageConfig> fileStorageOptions)
        {
            _pictureService = pictureService;
            _fileStorageConfig = fileStorageOptions.Value;
            _uploadPath = _fileStorageConfig.GetLocalUploadPath();
            
            // 确保上传目录存在
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        /// <summary>
        /// 获取图片列表
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="size">每页数量，默认为20</param>
        /// <param name="appType">应用类型搜索</param>
        /// <param name="imageName">图片名称搜索</param>
        /// <param name="imageCode">图片代码搜索</param>
        /// <returns>图片列表</returns>
        [HttpGet("list")]
        [ProducesResponseType(typeof(PagedResponse<PictureListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPictureList(
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            [FromQuery] string appType = "",
            [FromQuery] string imageName = "",
            [FromQuery] string imageCode = "")
        {
            try
            {
                var searchParams = new PictureSearchParams
                {
                    AppType = appType,
                    ImageName = imageName,
                    ImageCode = imageCode
                };

                var pictures = await _pictureService.GetPictureList(page, size, searchParams);
                var totalCount = await _pictureService.GetPictureCount(searchParams);
                var totalPages = (int)Math.Ceiling((double)totalCount / size);

                var response = new PagedResponse<PictureListDto>
                {
                    Data = pictures,
                    Total = totalCount,
                    Page = page,
                    PageSize = size,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取图片列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据应用类型获取图片列表
        /// </summary>
        /// <param name="appType">应用类型</param>
        /// <returns>图片列表</returns>
        [HttpGet("by-app-type/{appType}")]
        [ProducesResponseType(typeof(ApiResponse<PictureListDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPicturesByAppType(string appType)
        {
            try
            {
                var pictures = await _pictureService.GetPicturesByAppType(appType);
                return Ok(new ApiResponse<PictureListDto[]>
                {
                    Success = true,
                    Message = "获取图片列表成功",
                    Data = pictures.ToArray()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取图片列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取图片详情
        /// </summary>
        /// <param name="id">图片ID</param>
        /// <returns>图片详情</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<PictureListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPictureById(int id)
        {
            try
            {
                var picture = await _pictureService.GetPictureById(id);
                if (picture == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "图片不存在" });
                }

                var pictureDto = new PictureListDto
                {
                    Id = picture.Id,
                    AppType = picture.AppType,
                    ImageCode = picture.ImageCode,
                    ImageName = picture.ImageName,
                    ImagePath = picture.ImagePath,
                    AspectRatio = picture.AspectRatio,
                    Width = picture.Width,
                    Height = picture.Height,
                    SeqNo = picture.SeqNo,
                    CrtTime = picture.CrtTime,
                    UpdTime = picture.UpdTime
                };

                return Ok(new ApiResponse<PictureListDto>
                {
                    Success = true,
                    Message = "获取图片详情成功",
                    Data = pictureDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取图片详情失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 上传图片
        /// </summary>
        /// <param name="file">图片文件</param>
        /// <param name="aspectRatio">宽高比</param>
        /// <returns>上传结果</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(ApiResponse<UploadResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadImage(IFormFile file, [FromForm] string aspectRatio)
        {
            // 当使用远程访问时，优先代理到生产环境上传
            if (_fileStorageConfig.UseRemoteAccess)
            {
                try
                {
                    var remoteBase = $"http://{_fileStorageConfig.ServerHost}:{_fileStorageConfig.ServerPort}";
                    var remoteUploadUrl = $"{remoteBase}/api/picture/upload";
            
                    using var client = new HttpClient();
                    if (Request.Headers.TryGetValue("Authorization", out var auth))
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.ToString());
                    }
            
                    using var content = new MultipartFormDataContent();
                    using var stream = file.OpenReadStream();
                    var streamContent = new StreamContent(stream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    content.Add(streamContent, "file", file.FileName);
                    if (!string.IsNullOrEmpty(aspectRatio))
                    {
                        content.Add(new StringContent(aspectRatio), "aspectRatio");
                    }
            
                    var resp = await client.PostAsync(remoteUploadUrl, content);
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync();
                        // 反序列化为统一响应返回
                        var result = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<UploadResponse>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (result != null)
                        {
                            return Ok(result);
                        }
                        return Content(json, "application/json");
                    }
                    // 远程失败则回退到本地保存
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Remote upload failed: {ex.Message}");
                    // 回退到本地保存逻辑
                }
            }
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请选择要上传的图片文件" });
                }

                // 验证文件类型
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new ApiErrorResponse { Message = "只支持 JPG、PNG、GIF、BMP、WebP 格式的图片" });
                }

                // 验证文件大小（10MB）
                if (file.Length > 10 * 1024 * 1024)
                {
                    return BadRequest(new ApiErrorResponse { Message = "图片文件大小不能超过 10MB" });
                }

                // 使用GUID生成URL安全文件名，避免中文与特殊字符
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(_uploadPath, fileName);
                var accessUrl = _fileStorageConfig.GetImageUrl(fileName);

                // 保存文件
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 在上传图片方法中，修改获取图片尺寸的部分：
                
                // 获取图片尺寸信息
                int? width = null;
                int? height = null;
                string calculatedAspectRatio = "";

                try
                {
                    using (var image = Image.Load(filePath))
                    {
                        width = image.Width;
                        height = image.Height;
                        
                        // 计算最简宽高比
                        if (width.HasValue && height.HasValue && width.Value > 0 && height.Value > 0)
                        {
                            var gcd = GetGCD(width.Value, height.Value);
                            calculatedAspectRatio = $"{width.Value / gcd}:{height.Value / gcd}";
                        }
                    }
                }
                catch
                {
                    // 如果无法获取图片信息，返回错误
                    return BadRequest(new ApiErrorResponse { Message = "无法识别的图片格式" });
                }

                var response = new UploadResponse
                {
                    Url = accessUrl, // 使用配置的访问URL
                    FileName = fileName,
                    Size = file.Length,
                    Width = width,
                    Height = height,
                    AspectRatio = aspectRatio
                };

                return Ok(new ApiResponse<UploadResponse>
                {
                    Success = true,
                    Message = "图片上传成功",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "图片上传失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建新图片记录
        /// </summary>
        /// <param name="pictureDto">图片创建信息</param>
        /// <returns>创建结果</returns>
        [HttpPost("create")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePicture([FromBody] PictureDto pictureDto)
        {
            try
            {             
                if (pictureDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }
                
                // 移除自动生成ImagePath的逻辑，因为前端应该传递上传后的实际路径
                // 如果前端没有传递ImagePath，则返回错误
                if (string.IsNullOrEmpty(pictureDto.ImagePath))
                {
                    return BadRequest(new ApiErrorResponse { Message = "图片路径不能为空，请先上传图片" });
                }
                
                // 打印pictureDto数据到控制台
                Console.WriteLine($"CreatePicture - pictureDto数据: ImageCode={pictureDto.ImageCode}, ImageName={pictureDto.ImageName}, ImagePath={pictureDto.ImagePath}, AspectRatio={pictureDto.AspectRatio}");
                
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToList();
                    
                    var errorMessage = string.Join("; ", errors.SelectMany(e => e.Errors));
                    return BadRequest(new ApiErrorResponse { 
                        Message = "请求数据验证失败", 
                        Details = errorMessage 
                    });
                }

                var result = await _pictureService.CreatePicture(pictureDto);
                if (!result)
                {
                    return BadRequest(new ApiErrorResponse { Message = "图片代码已存在，请使用其他代码" });
                }

                return Ok(new ApiResponse { Message = "图片创建成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { 
                    Message = "创建图片失败", 
                    Details = ex.Message 
                });
            }
        }

        /// <summary>
        /// 更新图片
        /// </summary>
        /// <param name="id">图片ID</param>
        /// <param name="pictureDto">图片更新信息</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePicture(int id, [FromBody] PictureDto pictureDto)
        {
            try
            {
                if (pictureDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }

                var result = await _pictureService.UpdatePicture(id, pictureDto);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "图片不存在或图片代码已被使用" });
                }

                return Ok(new ApiResponse { Message = "图片更新成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "更新图片失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除图片
        /// </summary>
        /// <param name="id">图片ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePicture(int id)
        {
            try
            {
                var result = await _pictureService.DeletePicture(id);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "图片不存在" });
                }

                return Ok(new ApiResponse { Message = "图片删除成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除图片失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除图片文件
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>删除结果</returns>
        [HttpDelete("file")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteImageFile([FromQuery] string imagePath)
        {
            // 兼容完整URL：取 pathname 作为后端识别的相对路径
            string normalizedPath = imagePath;
            if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri))
            {
                normalizedPath = uri.AbsolutePath;
            }
        
            if (_fileStorageConfig.UseRemoteAccess)
            {
                try
                {
                    var remoteBase = $"http://{_fileStorageConfig.ServerHost}:{_fileStorageConfig.ServerPort}";
                    var remoteDeleteUrl = $"{remoteBase}/api/picture/file?imagePath={Uri.EscapeDataString(normalizedPath)}";
        
                    using var client = new HttpClient();
                    if (Request.Headers.TryGetValue("Authorization", out var auth))
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.ToString());
                    }
        
                    var resp = await client.DeleteAsync(remoteDeleteUrl);
                    var body = await resp.Content.ReadAsStringAsync();
                    return StatusCode((int)resp.StatusCode, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Remote delete failed: {ex.Message}");
                    // 转本地删除作为回退
                }
            }
        
            // 本地删除回退
            var result = await _pictureService.DeleteImageFile(normalizedPath);
            if (!result)
            {
                return BadRequest(new ApiErrorResponse { Message = "图片文件不存在或删除失败" });
            }
            return Ok(new ApiResponse { Message = "图片文件删除成功" });
        }

        /// <summary>
        /// 计算最大公约数
        /// </summary>
        private int GetGCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
    }
}