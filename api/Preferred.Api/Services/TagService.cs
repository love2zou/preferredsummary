using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;
using OfficeOpenXml;
using System.IO;
using System.Drawing;

namespace Preferred.Api.Services
{
    public class TagService : ITagService
    {
        private readonly ApplicationDbContext _context;

        public TagService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TagListDto>> GetTagList(int page = 1, int pageSize = 10, TagSearchParams searchParams = null)
        {
            var query = _context.Tags.AsQueryable();

            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.ParName))
                {
                    query = query.Where(t => t.ParName.Contains(searchParams.ParName));
                }
                if (!string.IsNullOrEmpty(searchParams.TagCode))
                {
                    query = query.Where(t => t.TagCode.Contains(searchParams.TagCode));
                }
                if (!string.IsNullOrEmpty(searchParams.TagName))
                {
                    query = query.Where(t => t.TagName.Contains(searchParams.TagName));
                }
            }

            var tags = await query
                .OrderBy(t => t.ParName)
                .ThenBy(t => t.SeqNo)
                .ThenBy(t => t.TagCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TagListDto
                {
                    Id = t.Id,
                    ParName = t.ParName,
                    TagCode = t.TagCode,
                    TagName = t.TagName,
                    HexColor = t.HexColor,
                    RgbColor = t.RgbColor,
                    SeqNo = t.SeqNo,
                    CrtTime = t.CrtTime,
                    UpdTime = t.UpdTime
                })
                .ToListAsync();

            return tags;
        }

        public async Task<int> GetTagCount(TagSearchParams searchParams = null)
        {
            var query = _context.Tags.AsQueryable();

            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.ParName))
                {
                    query = query.Where(t => t.ParName.Contains(searchParams.ParName));
                }
                if (!string.IsNullOrEmpty(searchParams.TagCode))
                {
                    query = query.Where(t => t.TagCode.Contains(searchParams.TagCode));
                }
                if (!string.IsNullOrEmpty(searchParams.TagName))
                {
                    query = query.Where(t => t.TagName.Contains(searchParams.TagName));
                }
            }

            return await query.CountAsync();
        }

        public async Task<Tag> GetTagById(int id)
        {
            return await _context.Tags.FindAsync(id);
        }

        public async Task<List<TagListDto>> GetTagsByParName(string parName)
        {
            return await _context.Tags
                .Where(t => t.ParName == parName)
                .OrderBy(t => t.SeqNo)
                .ThenBy(t => t.TagCode)
                .Select(t => new TagListDto
                {
                    Id = t.Id,
                    ParName = t.ParName,
                    TagCode = t.TagCode,
                    TagName = t.TagName,
                    HexColor = t.HexColor,
                    RgbColor = t.RgbColor,
                    SeqNo = t.SeqNo,
                    CrtTime = t.CrtTime,
                    UpdTime = t.UpdTime
                })
                .ToListAsync();
        }

        public async Task<bool> CreateTag(TagDto tagDto)
        {
            // 检查标签代码是否已存在
            if (await IsTagCodeExists(tagDto.ParName, tagDto.TagCode))
            {
                return false;
            }

            var tag = new Tag
            {
                ParName = tagDto.ParName,
                TagCode = tagDto.TagCode,
                TagName = tagDto.TagName,
                HexColor = tagDto.HexColor,
                RgbColor = tagDto.RgbColor,
                SeqNo = tagDto.SeqNo,
                CrtTime = DateTime.UtcNow,
                UpdTime = DateTime.UtcNow
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateTag(int id, TagDto tagDto)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
            {
                return false;
            }

            // 检查标签代码是否已存在（排除当前标签）
            if (await IsTagCodeExists(tagDto.ParName, tagDto.TagCode, id))
            {
                return false;
            }

            tag.ParName = tagDto.ParName;
            tag.TagCode = tagDto.TagCode;
            tag.TagName = tagDto.TagName;
            tag.HexColor = tagDto.HexColor;
            tag.RgbColor = tagDto.RgbColor;
            tag.SeqNo = tagDto.SeqNo;
            tag.UpdTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTag(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
            {
                return false;
            }

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsTagCodeExists(string parName, string tagCode, int? excludeId = null)
        {
            var query = _context.Tags.Where(t => t.ParName == parName && t.TagCode == tagCode);
            
            if (excludeId.HasValue)
            {
                query = query.Where(t => t.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<List<string>> GetParNameList()
        {
            var parNames = await _context.Tags
                .Select(t => t.ParName)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();
            
            // 如果没有数据，返回默认的应用模块
            if (!parNames.Any())
            {
                parNames.Add("访问地址");
            }
            
            return parNames;
        }

        /// <summary>
        /// 导入标签数据
        /// </summary>
        public async Task<List<TagImportResult>> ImportTagsFromExcel(Stream excelStream, IProgress<TagImportProgress> progress = null)
        {
            var results = new List<TagImportResult>();
            
            try
            {
                using (var package = new ExcelPackage(excelStream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        throw new InvalidOperationException("Excel文件中没有找到工作表");
                    }

                    var rowCount = worksheet.Dimension?.Rows ?? 0;
                    if (rowCount <= 1)
                    {
                        throw new InvalidOperationException("Excel文件中没有数据行");
                    }

                    // 获取现有的标签代码，用于重复检查
                    var existingTagCodes = await _context.Tags.Select(t => t.TagCode.ToLower()).ToListAsync();
                    var currentBatchCodes = new HashSet<string>();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var result = new TagImportResult { RowNumber = row };
                        
                        try
                        {
                            // 修改字段映射以匹配用户的Excel结构
                            // 跳过A列序号，从B列开始读取数据
                            result.ParName = worksheet.Cells[row, 2].Value?.ToString()?.Trim();   // B列：应用模块
                            result.TagCode = worksheet.Cells[row, 3].Value?.ToString()?.Trim();   // C列：标签代码
                            result.TagName = worksheet.Cells[row, 4].Value?.ToString()?.Trim();   // D列：标签名称
                            
                            if (int.TryParse(worksheet.Cells[row, 5].Value?.ToString(), out int seqNo)) // E列：排序号
                            {
                                result.SeqNo = seqNo;
                            }

                            // 数据验证
                            var validationResult = ValidateImportData(result, existingTagCodes, currentBatchCodes);
                            if (!validationResult.IsValid)
                            {
                                result.IsSuccess = false;
                                result.Message = validationResult.ErrorMessage;
                                results.Add(result);
                                continue;
                            }

                            // 生成颜色
                            var colors = GenerateTagColors();
                            result.HexColor = colors.HexColor;
                            result.RgbColor = colors.RgbColor;

                            // 创建标签实体
                            var tag = new Tag
                            {
                                ParName = result.ParName,
                                TagCode = result.TagCode,
                                TagName = result.TagName,
                                HexColor = result.HexColor,
                                RgbColor = result.RgbColor,
                                SeqNo = result.SeqNo,
                                CrtTime = DateTime.Now,
                                UpdTime = DateTime.Now
                            };

                            _context.Tags.Add(tag);
                            await _context.SaveChangesAsync();

                            currentBatchCodes.Add(result.TagCode.ToLower());
                            result.IsSuccess = true;
                            result.Message = "导入成功";
                        }
                        catch (Exception ex)
                        {
                            result.IsSuccess = false;
                            result.Message = $"导入失败：{ex.Message}";
                        }

                        results.Add(result);

                        // 更新进度
                        progress?.Report(new TagImportProgress
                        {
                            CurrentRow = row - 1,
                            TotalRows = rowCount - 1,
                            Percentage = (int)((double)(row - 1) / (rowCount - 1) * 100),
                            Status = $"正在处理第 {row - 1} 行，共 {rowCount - 1} 行"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"处理Excel文件时发生错误：{ex.Message}", ex);
            }

            return results;
        }

        /// <summary>
        /// 验证导入数据
        /// </summary>
        private (bool IsValid, string ErrorMessage) ValidateImportData(TagImportResult data, List<string> existingCodes, HashSet<string> currentBatchCodes)
        {
            if (string.IsNullOrWhiteSpace(data.ParName))
                return (false, "应用模块不能为空");
                
            if (string.IsNullOrWhiteSpace(data.TagCode))
                return (false, "标签代码不能为空");
                
            if (string.IsNullOrWhiteSpace(data.TagName))
                return (false, "标签名称不能为空");
                
            if (data.ParName.Length > 50)
                return (false, "应用模块长度不能超过50个字符");
                
            if (data.TagCode.Length > 20)
                return (false, "标签代码长度不能超过20个字符");
                
            if (data.TagName.Length > 50)
                return (false, "标签名称长度不能超过50个字符");
                
            if (existingCodes.Contains(data.TagCode.ToLower()))
                return (false, "标签代码已存在于数据库中");
                
            if (currentBatchCodes.Contains(data.TagCode.ToLower()))
                return (false, "标签代码在当前导入批次中重复");

            return (true, string.Empty);
        }

        /// <summary>
        /// 生成标签颜色
        /// </summary>
        private (string HexColor, string RgbColor) GenerateTagColors()
        {
            var random = new Random();
            
            // 预定义的高对比度基础颜色
            var baseColors = new[]
            {
                "#E74C3C", // 鲜红色
                "#3498DB", // 鲜蓝色
                "#2ECC71", // 鲜绿色
                "#F39C12", // 橙色
                "#9B59B6", // 紫色
                "#1ABC9C", // 青绿色
                "#E67E22", // 深橙色
                "#34495E", // 深蓝灰色
                "#C0392B", // 深红色
                "#27AE60", // 深绿色
                "#8E44AD", // 深紫色
                "#16A085", // 深青色
                "#D35400", // 深橙红色
                "#2C3E50", // 深灰蓝色
                "#F1C40F"  // 金黄色
            };
            
            string hexColor;
            
            // 如果基础颜色足够，直接使用
            if (baseColors.Length > 0)
            {
                // 80% 概率使用预定义颜色，20% 概率生成随机颜色
                if (random.NextDouble() < 0.8)
                {
                    hexColor = baseColors[random.Next(baseColors.Length)];
                }
                else
                {
                    // 动态生成高饱和度颜色
                    hexColor = GenerateRandomHighContrastColor(random);
                }
            }
            else
            {
                hexColor = GenerateRandomHighContrastColor(random);
            }
            
            // 将HEX转换为RGB并添加0.15透明度
            var color = ColorTranslator.FromHtml(hexColor);
            var rgbColor = $"rgba({color.R}, {color.G}, {color.B}, 0.15)";
            
            return (hexColor, rgbColor);
        }

        /// <summary>
        /// 生成随机高对比度颜色
        /// </summary>
        private string GenerateRandomHighContrastColor(Random random)
        {
            // 生成高饱和度、中等亮度的颜色
            // 使用HSL色彩空间确保颜色鲜艳且对比度高
            
            // 色相：0-360度，随机选择
            var hue = random.Next(0, 360);
            
            // 饱和度：70-100%，确保颜色鲜艳
            var saturation = random.Next(70, 101);
            
            // 亮度：40-70%，确保既不太暗也不太亮
            var lightness = random.Next(40, 71);
            
            // 将HSL转换为RGB
            var (r, g, b) = HslToRgb(hue, saturation, lightness);
            
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// HSL转RGB
        /// </summary>
        private (int r, int g, int b) HslToRgb(int h, int s, int l)
        {
            var hNorm = h / 360.0;
            var sNorm = s / 100.0;
            var lNorm = l / 100.0;
            
            double r, g, b;
            
            if (sNorm == 0)
            {
                r = g = b = lNorm; // 灰色
            }
            else
            {
                var hue2rgb = new Func<double, double, double, double>((p, q, t) =>
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0/6) return p + (q - p) * 6 * t;
                    if (t < 1.0/2) return q;
                    if (t < 2.0/3) return p + (q - p) * (2.0/3 - t) * 6;
                    return p;
                });
                
                var q = lNorm < 0.5 ? lNorm * (1 + sNorm) : lNorm + sNorm - lNorm * sNorm;
                var p = 2 * lNorm - q;
                
                r = hue2rgb(p, q, hNorm + 1.0/3);
                g = hue2rgb(p, q, hNorm);
                b = hue2rgb(p, q, hNorm - 1.0/3);
            }
            
            return ((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
        }

        /// <summary>
        /// 生成导入结果Excel文件
        /// </summary>
        public byte[] GenerateImportResultExcel(List<TagImportResult> results)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("导入结果");
                
                // 设置标题行
                worksheet.Cells[1, 1].Value = "行号";
                worksheet.Cells[1, 2].Value = "应用模块";
                worksheet.Cells[1, 3].Value = "标签代码";
                worksheet.Cells[1, 4].Value = "标签名称";
                worksheet.Cells[1, 5].Value = "排序号";
                worksheet.Cells[1, 6].Value = "字体颜色";
                worksheet.Cells[1, 7].Value = "背景颜色";
                worksheet.Cells[1, 8].Value = "是否成功";
                worksheet.Cells[1, 9].Value = "提示信息";
                
                // 填充数据
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    var row = i + 2;
                    
                    worksheet.Cells[row, 1].Value = result.RowNumber;
                    worksheet.Cells[row, 2].Value = result.ParName;
                    worksheet.Cells[row, 3].Value = result.TagCode;
                    worksheet.Cells[row, 4].Value = result.TagName;
                    worksheet.Cells[row, 5].Value = result.SeqNo;
                    worksheet.Cells[row, 6].Value = result.HexColor;
                    worksheet.Cells[row, 7].Value = result.RgbColor;
                    worksheet.Cells[row, 8].Value = result.IsSuccess ? "成功" : "失败";
                    worksheet.Cells[row, 9].Value = result.Message;
                }
                
                // 自动调整列宽
                worksheet.Cells.AutoFitColumns();
                
                return package.GetAsByteArray();
            }
        }

        /// <summary>
        /// 批量删除标签
        /// </summary>
        /// <param name="ids">标签ID数组</param>
        /// <returns>删除结果</returns>
        public async Task<BatchDeleteResult> BatchDeleteTags(int[] ids)
        {
            var result = new BatchDeleteResult
            {
                SuccessCount = 0,
                FailCount = 0,
                FailedIds = new List<int>()
            };

            foreach (var id in ids)
            {
                try
                {
                    var tag = await _context.Tags.FindAsync(id);
                    if (tag != null)
                    {
                        _context.Tags.Remove(tag);
                        await _context.SaveChangesAsync();
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailCount++;
                        result.FailedIds.Add(id);
                    }
                }
                catch (Exception)
                {
                    result.FailCount++;
                    result.FailedIds.Add(id);
                }
            }

            return result;
        }
    }
}