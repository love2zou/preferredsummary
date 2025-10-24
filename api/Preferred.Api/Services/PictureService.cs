using System;
using System.Collections.Generic;
using System.IO;  // 新增
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 图片数据服务实现
    /// </summary>
    public class PictureService : IPictureService
    {
        private readonly ApplicationDbContext _context;

        public PictureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PictureListDto>> GetPictureList(int page, int size, PictureSearchParams searchParams)
        {
            var query = _context.Pictures.AsQueryable();

            // 应用搜索条件
            if (!string.IsNullOrEmpty(searchParams?.AppType))
            {
                query = query.Where(p => p.AppType.Contains(searchParams.AppType));
            }

            if (!string.IsNullOrEmpty(searchParams?.ImageName))
            {
                query = query.Where(p => p.ImageName.Contains(searchParams.ImageName));
            }

            if (!string.IsNullOrEmpty(searchParams?.ImageCode))
            {
                query = query.Where(p => p.ImageCode.Contains(searchParams.ImageCode));
            }

            var pictures = await query
                .OrderByDescending(p => p.CrtTime)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(p => new PictureListDto
                {
                    Id = p.Id,
                    AppType = p.AppType,
                    ImageCode = p.ImageCode,
                    ImageName = p.ImageName,
                    ImagePath = p.ImagePath,
                    AspectRatio = p.AspectRatio,
                    Width = p.Width,
                    Height = p.Height,
                    SeqNo = p.SeqNo,
                    CrtTime = p.CrtTime,
                    UpdTime = p.UpdTime
                })
                .ToListAsync();

            return pictures;
        }

        public async Task<int> GetPictureCount(PictureSearchParams searchParams)
        {
            var query = _context.Pictures.AsQueryable();

            // 应用搜索条件
            if (!string.IsNullOrEmpty(searchParams?.AppType))
            {
                query = query.Where(p => p.AppType.Contains(searchParams.AppType));
            }

            if (!string.IsNullOrEmpty(searchParams?.ImageName))
            {
                query = query.Where(p => p.ImageName.Contains(searchParams.ImageName));
            }

            if (!string.IsNullOrEmpty(searchParams?.ImageCode))
            {
                query = query.Where(p => p.ImageCode.Contains(searchParams.ImageCode));
            }

            return await query.CountAsync();
        }

        public async Task<List<PictureListDto>> GetPicturesByAppType(string appType)
        {
            var pictures = await _context.Pictures
                .Where(p => p.AppType == appType)
                .OrderBy(p => p.SeqNo)
                .ThenByDescending(p => p.CrtTime)
                .Select(p => new PictureListDto
                {
                    Id = p.Id,
                    AppType = p.AppType,
                    ImageCode = p.ImageCode,
                    ImageName = p.ImageName,
                    ImagePath = p.ImagePath,
                    AspectRatio = p.AspectRatio,
                    Width = p.Width,
                    Height = p.Height,
                    SeqNo = p.SeqNo,
                    CrtTime = p.CrtTime,
                    UpdTime = p.UpdTime
                })
                .ToListAsync();

            return pictures;
        }

        public async Task<Picture> GetPictureById(int id)
        {
            return await _context.Pictures.FindAsync(id);
        }

        public async Task<bool> CreatePicture(PictureDto pictureDto)
        {
            // 检查图片代码是否已存在
            if (await _context.Pictures.AnyAsync(p => p.ImageCode == pictureDto.ImageCode))
            {
                return false;
            }

            // 优先使用前端传递的 AspectRatio，如果为 0 或无效，则尝试解析 AspectRatioString
            decimal aspectRatioValue = pictureDto.AspectRatio;
            
            if (aspectRatioValue == 0 && !string.IsNullOrEmpty(pictureDto.AspectRatioString))
            {
                var parts = pictureDto.AspectRatioString.Split(':');
                if (parts.Length == 2 && decimal.TryParse(parts[0], out decimal w) && decimal.TryParse(parts[1], out decimal h) && h != 0)
                {
                    aspectRatioValue = w / h;
                }
            }
            
            // 如果还是 0，且有宽高信息，则计算宽高比
            if (aspectRatioValue == 0 && pictureDto.Width.HasValue && pictureDto.Height.HasValue && pictureDto.Height.Value != 0)
            {
                aspectRatioValue = (decimal)pictureDto.Width.Value / pictureDto.Height.Value;
            }

            var picture = new Picture
            {
                AppType = pictureDto.AppType,
                ImageCode = pictureDto.ImageCode,
                ImageName = pictureDto.ImageName,
                ImagePath = pictureDto.ImagePath,
                AspectRatio = aspectRatioValue,
                Width = pictureDto.Width,
                Height = pictureDto.Height,
                SeqNo = pictureDto.SeqNo,
                CrtTime = DateTime.Now,
                UpdTime = DateTime.Now
            };

            _context.Pictures.Add(picture);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePicture(int id, PictureDto pictureDto)
        {
            var picture = await _context.Pictures.FindAsync(id);
            if (picture == null)
            {
                return false;
            }

            // 检查图片代码是否已被其他记录使用
            if (await IsImageCodeExists(pictureDto.ImageCode, id))
            {
                return false;
            }

            picture.AppType = pictureDto.AppType;
            picture.ImageCode = pictureDto.ImageCode;
            picture.ImageName = pictureDto.ImageName;
            picture.ImagePath = pictureDto.ImagePath;
            picture.AspectRatio = pictureDto.AspectRatio;
            picture.Width = pictureDto.Width;
            picture.Height = pictureDto.Height;
            picture.SeqNo = pictureDto.SeqNo;
            picture.UpdTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePicture(int id)
        {
            var picture = await _context.Pictures.FindAsync(id);
            if (picture == null)
            {
                return false;
            }

            // 删除数据库记录
            _context.Pictures.Remove(picture);
            await _context.SaveChangesAsync();
            
            // 删除服务器上的图片文件
            if (!string.IsNullOrEmpty(picture.ImagePath))
            {
                try
                {
                    // 构建完整的文件路径
                    var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var filePath = Path.Combine(webRootPath, picture.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    
                    // 检查文件是否存在并删除
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Console.WriteLine($"已删除图片文件: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine($"图片文件不存在: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误但不影响数据库删除操作
                    Console.WriteLine($"删除图片文件时发生错误: {ex.Message}");
                }
            }
            
            return true;
        }

        public async Task<bool> IsImageCodeExists(string imageCode, int? excludeId = null)
        {
            var query = _context.Pictures.Where(p => p.ImageCode == imageCode);
            
            if (excludeId.HasValue)
            {
                query = query.Where(p => p.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> DeleteImageFile(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                return false;
            }

            try
            {
                string relativePath;
                
                // 判断是否为HTTP URL
                if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://"))
                {
                    // 从HTTP URL中提取相对路径
                    var uri = new Uri(imagePath);
                    relativePath = uri.AbsolutePath; // 例如：/upload/images/avatar20251024235833.jpg
                }
                else
                {
                    // 已经是相对路径
                    relativePath = imagePath;
                }
                
                // 构建完整的文件路径
                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var filePath = Path.Combine(webRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                
                // 检查文件是否存在并删除
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine($"已删除图片文件: {filePath}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"图片文件不存在: {filePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除图片文件时发生错误: {ex.Message}");
                return false;
            }
        }
    }
}