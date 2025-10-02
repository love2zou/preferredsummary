using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 分类管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] // 需要JWT认证
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// 获取分类列表
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="size">每页数量，默认为10</param>
        /// <param name="categoryCode">分类代码搜索</param>
        /// <param name="categoryName">分类名称搜索</param>
        /// <param name="description">描述搜索</param>
        /// <returns>分类列表</returns>
        [HttpGet("list")]
        [ProducesResponseType(typeof(PagedResponse<CategoryListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategoryList(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string categoryCode = "",
            [FromQuery] string categoryName = "",
            [FromQuery] string description = "")
        {
            try
            {
                var searchParams = new CategorySearchParams
                {
                    CategoryCode = categoryCode,
                    CategoryName = categoryName,
                    Description = description
                };

                var categories = await _categoryService.GetCategoryList(page, size, searchParams);
                var totalCount = await _categoryService.GetCategoryCount(searchParams);
                var totalPages = (int)Math.Ceiling((double)totalCount / size);

                var response = new PagedResponse<CategoryListDto>
                {
                    Data = categories,
                    Total = totalCount,
                    Page = page,
                    PageSize = size,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取分类列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取分类详情
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <returns>分类详情</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            try
            {
                var category = await _categoryService.GetCategoryById(id);
                if (category == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "分类不存在" });
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取分类详情失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建新分类
        /// </summary>
        /// <param name="categoryDto">分类创建信息</param>
        /// <returns>创建结果</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto categoryDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // 检查分类代码是否已存在
                var codeExists = await _categoryService.IsCategoryCodeExists(categoryDto.CategoryCode);
                if (codeExists)
                {
                    return BadRequest(new ApiErrorResponse { Message = "分类代码已存在" });
                }

                var success = await _categoryService.CreateCategory(categoryDto);
                if (success)
                {
                    return CreatedAtAction(nameof(GetCategoryById), new { id = 0 }, new ApiResponse { Success = true, Message = "分类创建成功" });
                }
                else
                {
                    return StatusCode(500, new ApiErrorResponse { Message = "分类创建失败" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "分类创建失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 更新分类
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <param name="categoryDto">分类更新信息</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto categoryDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // 检查分类是否存在
                var existingCategory = await _categoryService.GetCategoryById(id);
                if (existingCategory == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "分类不存在" });
                }

                // 检查分类代码是否已存在（排除当前分类）
                var codeExists = await _categoryService.IsCategoryCodeExists(categoryDto.CategoryCode, id);
                if (codeExists)
                {
                    return BadRequest(new ApiErrorResponse { Message = "分类代码已存在" });
                }

                var success = await _categoryService.UpdateCategory(id, categoryDto);
                if (success)
                {
                    return Ok(new ApiResponse { Success = true, Message = "分类更新成功" });
                }
                else
                {
                    return StatusCode(500, new ApiErrorResponse { Message = "分类更新失败" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "分类更新失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除分类
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _categoryService.GetCategoryById(id);
                if (category == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "分类不存在" });
                }

                var success = await _categoryService.DeleteCategory(id);
                if (success)
                {
                    return Ok(new ApiResponse { Success = true, Message = "分类删除成功" });
                }
                else
                {
                    return StatusCode(500, new ApiErrorResponse { Message = "分类删除失败" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "分类删除失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 获取所有分类代码列表
        /// </summary>
        /// <returns>分类代码列表</returns>
        [HttpGet("codes")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategoryCodes()
        {
            try
            {
                var codes = await _categoryService.GetCategoryCodeList();
                return Ok(codes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取分类代码列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 检查分类代码是否存在
        /// </summary>
        /// <param name="categoryCode">分类代码</param>
        /// <param name="excludeId">排除的ID</param>
        /// <returns>是否存在</returns>
        [HttpGet("check-code")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckCategoryCodeExists([FromQuery] string categoryCode, [FromQuery] int? excludeId = null)
        {
            try
            {
                var exists = await _categoryService.IsCategoryCodeExists(categoryCode, excludeId);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "检查分类代码失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 批量删除分类
        /// </summary>
        /// <param name="request">批量删除请求</param>
        /// <returns>删除结果</returns>
        [HttpDelete("batch")]
        [ProducesResponseType(typeof(ApiResponse<BatchDeleteResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> BatchDeleteCategories([FromBody] BatchDeleteRequest request)
        {
            try
            {
                if (request?.Ids == null || request.Ids.Length == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请选择要删除的分类" });
                }

                var result = await _categoryService.BatchDeleteCategories(request.Ids);
                return Ok(new ApiResponse<BatchDeleteResult>
                {
                    Success = true,
                    Message = $"批量删除完成，成功：{result.SuccessCount}，失败：{result.FailCount}",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "批量删除失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 更新分类排序
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <param name="request">排序更新请求</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}/seqno")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategorySeqNo(int id, [FromBody] SeqNoUpdateRequest request)
        {
            try
            {
                var category = await _categoryService.GetCategoryById(id);
                if (category == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "分类不存在" });
                }

                var success = await _categoryService.UpdateCategorySeqNo(id, request.SeqNo);
                if (success)
                {
                    return Ok(new ApiResponse { Success = true, Message = "排序更新成功" });
                }
                else
                {
                    return StatusCode(500, new ApiErrorResponse { Message = "排序更新失败" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "排序更新失败", Details = ex.Message });
            }
        }
    }

    /// <summary>
    /// 排序更新请求模型
    /// </summary>
    public class SeqNoUpdateRequest
    {
        /// <summary>
        /// 新的排序号
        /// </summary>
        public int SeqNo { get; set; }
    }
}