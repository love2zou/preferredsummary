using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;
using System.IO;
using OfficeOpenXml;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Collections.Generic; // 添加这行

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 标签管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] // 需要JWT认证
    public class TagController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagController(ITagService tagService)
        {
            _tagService = tagService;
            // EPPlus 5.x 版本的许可证设置
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// 获取标签列表
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="size">每页数量，默认为10</param>
        /// <param name="parName">应用模块搜索</param>
        /// <param name="tagCode">标签代码搜索</param>
        /// <param name="tagName">标签名称搜索</param>
        /// <returns>标签列表</returns>
        [HttpGet("list")]
        [ProducesResponseType(typeof(PagedResponse<TagListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTagList(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string parName = "",
            [FromQuery] string tagCode = "",
            [FromQuery] string tagName = "")
        {
            try
            {
                var searchParams = new TagSearchParams
                {
                    ParName = parName,
                    TagCode = tagCode,
                    TagName = tagName
                };

                var tags = await _tagService.GetTagList(page, size, searchParams);
                var totalCount = await _tagService.GetTagCount(searchParams);
                var totalPages = (int)Math.Ceiling((double)totalCount / size);

                var response = new PagedResponse<TagListDto>
                {
                    Data = tags,
                    Total = totalCount,
                    Page = page,
                    PageSize = size,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取标签列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据应用模块获取标签列表
        /// </summary>
        /// <param name="parName">应用模块名称</param>
        /// <returns>标签列表</returns>
        [HttpGet("by-module/{parName}")]
        [ProducesResponseType(typeof(ApiResponse<TagListDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTagsByParName(string parName)
        {
            try
            {
                var tags = await _tagService.GetTagsByParName(parName);
                return Ok(new ApiResponse<TagListDto[]>
                {
                    Success = true,
                    Message = "获取标签列表成功",
                    Data = tags.ToArray()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取标签列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取标签详情
        /// </summary>
        /// <param name="id">标签ID</param>
        /// <returns>标签详情</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<TagListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTagById(int id)
        {
            try
            {
                var tag = await _tagService.GetTagById(id);
                if (tag == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "标签不存在" });
                }

                var tagDto = new TagListDto
                {
                    Id = tag.Id,
                    ParName = tag.ParName,
                    TagCode = tag.TagCode,
                    TagName = tag.TagName,
                    HexColor = tag.HexColor,
                    RgbColor = tag.RgbColor,
                    SeqNo = tag.SeqNo,
                    CrtTime = tag.CrtTime,
                    UpdTime = tag.UpdTime
                };

                return Ok(new ApiResponse<TagListDto>
                {
                    Success = true,
                    Message = "获取标签详情成功",
                    Data = tagDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取标签详情失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建新标签
        /// </summary>
        /// <param name="tagDto">标签创建信息</param>
        /// <returns>创建结果</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTag([FromBody] TagDto tagDto)
        {
            try
            {
                if (tagDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }

                var result = await _tagService.CreateTag(tagDto);
                if (!result)
                {
                    return BadRequest(new ApiErrorResponse { Message = "标签代码已存在，请使用其他代码" });
                }

                return Ok(new ApiResponse { Message = "标签创建成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建标签失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 更新标签
        /// </summary>
        /// <param name="id">标签ID</param>
        /// <param name="tagDto">标签更新信息</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTag(int id, [FromBody] TagDto tagDto)
        {
            try
            {
                if (tagDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }

                var result = await _tagService.UpdateTag(id, tagDto);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "标签不存在或标签代码已被使用" });
                }

                return Ok(new ApiResponse { Message = "标签更新成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "更新标签失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除标签
        /// </summary>
        /// <param name="id">标签ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTag(int id)
        {
            try
            {
                var result = await _tagService.DeleteTag(id);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "标签不存在" });
                }

                return Ok(new ApiResponse { Message = "标签删除成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除标签失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 获取所有应用模块列表
        /// </summary>
        /// <returns>应用模块列表</returns>
        [HttpGet("modules")]
        [ProducesResponseType(typeof(ApiResponse<string[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetModules()
        {
            try
            {
                var modules = await _tagService.GetParNameList();
                return Ok(new ApiResponse<string[]>
                {
                    Success = true,
                    Message = "获取模块列表成功",
                    Data = modules.ToArray()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取模块列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 下载标签导入模板
        /// </summary>
        /// <returns>Excel模板文件</returns>
        [HttpGet("download-template")]
        public IActionResult DownloadTemplate()
        {
            try
            {
                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "template", "标签数据导入模板.xlsx");
                
                if (!System.IO.File.Exists(templatePath))
                {
                    // 如果模板文件不存在，动态生成一个
                    var templateBytes = GenerateTemplate();
                    return File(templateBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "标签数据导入模板.xlsx");
                }
                
                var fileBytes = System.IO.File.ReadAllBytes(templatePath);
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "标签数据导入模板.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "下载模板失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 导入标签数据
        /// </summary>
        /// <param name="file">Excel文件</param>
        /// <returns>导入结果</returns>
        [HttpPost("import")]
        public async Task<IActionResult> ImportTags(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiErrorResponse { Message = "请选择要导入的Excel文件" });
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ApiErrorResponse { Message = "只支持.xlsx格式的Excel文件" });
            }

            try
            {
                List<TagImportResult> results = new List<TagImportResult>();
                
                using (var stream = file.OpenReadStream())
                {
                    var progress = new Progress<TagImportProgress>(p => 
                    {
                        // 这里可以通过SignalR推送进度给前端
                        Console.WriteLine($"导入进度: {p.Percentage}% - {p.Status}");
                    });
                    
                    results = await _tagService.ImportTagsFromExcel(stream, progress);
                }
                
                // 生成结果文件
                var resultBytes = _tagService.GenerateImportResultExcel(results);
                var resultFileName = $"标签导入结果_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                
                // 统计结果
                var successCount = results.Count(r => r.IsSuccess);
                var failCount = results.Count(r => !r.IsSuccess);
                
                // 将字节数组转换为Base64字符串
                var resultFileBase64 = Convert.ToBase64String(resultBytes);
                
                var response = new ApiResponse<object>
                {
                    Success = true,
                    Message = $"导入完成！成功：{successCount}条，失败：{failCount}条",
                    Data = new
                    {
                        TotalCount = results.Count,
                        SuccessCount = successCount,
                        FailCount = failCount,
                        ResultFile = resultFileBase64, // 使用变量而不是方法组
                        ResultFileName = resultFileName
                    }
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "导入失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 生成导入模板
        /// </summary>
        private byte[] GenerateTemplate()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("数据填充");
                
                // 设置标题行（不包含序号列）
                worksheet.Cells[1, 1].Value = "应用模块";
                worksheet.Cells[1, 2].Value = "标签代码";
                worksheet.Cells[1, 3].Value = "标签名称";
                worksheet.Cells[1, 4].Value = "排序号";
                
                // 示例数据
                // 示例数据
                worksheet.Cells[2, 1].Value = "访问地址";
                worksheet.Cells[2, 2].Value = "vue";
                worksheet.Cells[2, 3].Value = "设计";
                worksheet.Cells[2, 4].Value = 1;
                
                worksheet.Cells[3, 1].Value = "访问地址";  // 修正：应该是应用模块，不是数字2
                worksheet.Cells[3, 2].Value = "sheji";
                worksheet.Cells[3, 3].Value = "工具";
                worksheet.Cells[3, 4].Value = 2;
                
                // 设置列宽
                worksheet.Column(1).Width = 8;   // 序号
                worksheet.Column(2).Width = 20;  // 应用模块
                worksheet.Column(3).Width = 15;  // 标签代码
                worksheet.Column(4).Width = 20;  // 标签名称
                worksheet.Column(5).Width = 10;  // 排序号
                
                // 设置标题行样式
                using (var range = worksheet.Cells[1, 1, 1, 4])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                
                return package.GetAsByteArray();
            }
        }

        /// <summary>
        /// 批量删除标签
        /// </summary>
        /// <param name="ids">标签ID数组</param>
        /// <returns>删除结果</returns>
        [HttpDelete("batch")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BatchDeleteTags([FromBody] int[] ids)
        {
            try
            {
                if (ids == null || ids.Length == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请选择要删除的标签" });
                }

                var result = await _tagService.BatchDeleteTags(ids);
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"批量删除完成！成功删除 {result.SuccessCount} 条，失败 {result.FailCount} 条",
                    Data = new
                    {
                        TotalCount = ids.Length,
                        SuccessCount = result.SuccessCount,
                        FailCount = result.FailCount,
                        FailedIds = result.FailedIds
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "批量删除失败", Details = ex.Message });
            }
        }
    }
}