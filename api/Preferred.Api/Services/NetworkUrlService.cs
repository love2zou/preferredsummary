using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 访问地址服务实现
    /// </summary>
    public class NetworkUrlService : INetworkUrlService
    {
        private readonly ApplicationDbContext _context;

        public NetworkUrlService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<NetworkUrlListDto>> GetNetworkUrlList(int page, int size, NetworkUrlSearchParams searchParams)
        {
            // 先获取基础数据，不进行Category JOIN
            var query = from n in _context.NetworkUrls
                       join p in _context.Pictures on n.ImageCode equals p.ImageCode into pictureGroup
                       from picture in pictureGroup.DefaultIfEmpty()
                       select new { NetworkUrl = n, Picture = picture };

            if (!string.IsNullOrEmpty(searchParams?.Name))
                query = query.Where(x => x.NetworkUrl.Name.Contains(searchParams.Name));

            // 修改CategoryCode过滤逻辑，支持逗号分隔的多分类
            if (!string.IsNullOrEmpty(searchParams?.CategoryCode))
                query = query.Where(x => x.NetworkUrl.CategoryCode.Contains(searchParams.CategoryCode));

            if (searchParams?.IsMark.HasValue == true)
                query = query.Where(x => x.NetworkUrl.IsMark == searchParams.IsMark.Value);

            if (searchParams?.IsAvailable.HasValue == true)
                query = query.Where(x => x.NetworkUrl.IsAvailable == searchParams.IsAvailable.Value);

            // 先分页获取基础数据
            var baseList = await query
                .OrderBy(x => x.NetworkUrl.SeqNo)
                .ThenByDescending(x => x.NetworkUrl.CrtTime)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            // 解析所有分类代码
            var allCategoryCodes = baseList
                .SelectMany(x => x.NetworkUrl.CategoryCode.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Distinct()
                .ToList();

            // 批量查询分类信息
            var categories = await _context.Categories
                .Where(c => allCategoryCodes.Contains(c.CategoryCode))
                .ToListAsync();

            // 解析所有标签代码
            var allTagCodes = baseList
                .SelectMany(x => x.NetworkUrl.TagCodeType.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Distinct()
                .ToList();

            // 批量查询标签信息
            var tags = await _context.Tags
                .Where(t => allTagCodes.Contains(t.TagCode))
                .ToListAsync();

            // 组装结果
            var result = baseList.Select(x => {
                // 处理多个分类
                var categoryCodes = x.NetworkUrl.CategoryCode.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var categoryInfos = categories
                    .Where(c => categoryCodes.Contains(c.CategoryCode))
                    .ToList();
                
                // 取第一个分类作为主分类（或者根据业务需求调整）
                var primaryCategory = categoryInfos.FirstOrDefault();
                
                var tagCodes = x.NetworkUrl.TagCodeType.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var tagInfos = tags
                    .Where(t => tagCodes.Contains(t.TagCode))
                    .Select(t => new TagInfo {
                        CodeType = t.TagCode,
                        Name = t.TagName,
                        HexColor = t.HexColor,
                        RgbColor = t.RgbColor
                    })
                    .ToList();

                return new NetworkUrlListDto {
                    Id = x.NetworkUrl.Id,
                    Url = x.NetworkUrl.Url,
                    Name = x.NetworkUrl.Name,
                    Description = x.NetworkUrl.Description,
                    ImageCode = x.NetworkUrl.ImageCode,
                    CategoryCode = x.NetworkUrl.CategoryCode,
                    IsAvailable = x.NetworkUrl.IsAvailable,
                    IsMark = x.NetworkUrl.IsMark,
                    TagCodeType = x.NetworkUrl.TagCodeType,
                    SeqNo = x.NetworkUrl.SeqNo,
                    CrtTime = x.NetworkUrl.CrtTime,
                    UpdTime = x.NetworkUrl.UpdTime,
                    PictureUrl = x.Picture?.ImagePath,
                    PictureName = x.Picture?.ImageName,
                    CategoryName = primaryCategory?.CategoryName,
                    CategoryIcon = primaryCategory?.CategoryIcon,
                    CategoryDescription = primaryCategory?.Description,
                    Tags = tagInfos
                };
            }).ToList();

            return result;
        }

        public async Task<int> GetNetworkUrlCount(NetworkUrlSearchParams searchParams)
        {
            var query = _context.NetworkUrls.AsQueryable();

            if (!string.IsNullOrEmpty(searchParams?.Name))
            {
                query = query.Where(n => n.Name.Contains(searchParams.Name));
            }

            if (searchParams?.TagCodeTypes != null && searchParams.TagCodeTypes.Any())
            {
                query = query.Where(n => searchParams.TagCodeTypes.Contains(n.TagCodeType));
            }

            if (!string.IsNullOrEmpty(searchParams?.CategoryCode))
            {
                query = query.Where(n => n.CategoryCode == searchParams.CategoryCode);
            }

            if (searchParams?.IsMark.HasValue == true)
            {
                query = query.Where(n => n.IsMark == searchParams.IsMark.Value);
            }

            if (searchParams?.IsAvailable.HasValue == true)
            {
                query = query.Where(n => n.IsAvailable == searchParams.IsAvailable.Value);
            }

            return await query.CountAsync();
        }

        public async Task<List<NetworkUrlListDto>> GetNetworkUrlsByTagType(string tagCodeType)
        {
            var networkUrls = await _context.NetworkUrls
                .Where(n => n.TagCodeType == tagCodeType)
                .OrderBy(n => n.SeqNo)
                .ThenByDescending(n => n.CrtTime)
                .Select(n => new NetworkUrlListDto
                {
                    Id = n.Id,
                    Url = n.Url,
                    Name = n.Name,
                    Description = n.Description,
                    ImageCode = n.ImageCode,
                    CategoryCode = n.CategoryCode,
                    IsAvailable = n.IsAvailable,
                    IsMark = n.IsMark,
                    TagCodeType = n.TagCodeType,
                    SeqNo = n.SeqNo,
                    CrtTime = n.CrtTime,
                    UpdTime = n.UpdTime
                })
                .ToListAsync();

            return networkUrls;
        }

        public async Task<bool> CreateNetworkUrl(NetworkUrlDto networkUrlDto)
        {
            try
            {
                // 清理 URL 中的空格和特殊字符
                networkUrlDto.Url = networkUrlDto.Url?.Trim().Replace("`", "");
                
                // 检查URL是否已存在
                if (await _context.NetworkUrls.AnyAsync(n => n.Url == networkUrlDto.Url))
                {
                    return false;
                }

                var networkUrl = new NetworkUrl
                {
                    Url = networkUrlDto.Url,
                    Name = networkUrlDto.Name,
                    Description = networkUrlDto.Description,
                    ImageCode = networkUrlDto.ImageCode,
                    CategoryCode = networkUrlDto.CategoryCode,
                    IsAvailable = networkUrlDto.IsAvailable,
                    IsMark = networkUrlDto.IsMark,
                    TagCodeType = networkUrlDto.TagCodeType,
                    SeqNo = networkUrlDto.SeqNo,
                    CrtTime = DateTime.Now,
                    UpdTime = DateTime.Now
                };

                _context.NetworkUrls.Add(networkUrl);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // 记录详细错误信息
                Console.WriteLine($"CreateNetworkUrl Error: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                throw; // 重新抛出异常以便上层处理
            }
        }

        public async Task<bool> UpdateNetworkUrl(int id, NetworkUrlDto networkUrlDto)
        {
            var networkUrl = await _context.NetworkUrls.FindAsync(id);
            if (networkUrl == null)
            {
                return false;
            }

            // 检查URL是否已被其他记录使用
            if (await IsUrlExists(networkUrlDto.Url, id))
            {
                return false;
            }

            networkUrl.Url = networkUrlDto.Url;
            networkUrl.Name = networkUrlDto.Name;
            networkUrl.Description = networkUrlDto.Description;
            networkUrl.ImageCode = networkUrlDto.ImageCode;
            networkUrl.CategoryCode = networkUrlDto.CategoryCode;
            networkUrl.IsAvailable = networkUrlDto.IsAvailable;
            networkUrl.IsMark = networkUrlDto.IsMark;
            networkUrl.TagCodeType = networkUrlDto.TagCodeType;
            networkUrl.SeqNo = networkUrlDto.SeqNo;
            networkUrl.UpdTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<NetworkUrl> GetNetworkUrlById(int id)
        {
            return await _context.NetworkUrls.FindAsync(id);
        }

        public async Task<bool> DeleteNetworkUrl(int id)
        {
            var networkUrl = await _context.NetworkUrls.FindAsync(id);
            if (networkUrl == null)
            {
                return false;
            }

            _context.NetworkUrls.Remove(networkUrl);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsUrlExists(string url, int? excludeId = null)
        {
            var query = _context.NetworkUrls.Where(n => n.Url == url);
            
            if (excludeId.HasValue)
            {
                query = query.Where(n => n.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}