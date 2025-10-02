using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 访问地址管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] // 需要JWT认证
    public class NetworkUrlController : ControllerBase
    {
        private readonly INetworkUrlService _networkUrlService;

        public NetworkUrlController(INetworkUrlService networkUrlService)
        {
            _networkUrlService = networkUrlService;
        }

        /// <summary>
        /// 获取访问地址列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页数量</param>
        /// <param name="name">名称</param>
        /// <param name="tagCodeTypes">标签类型列表（逗号分隔）</param>
        /// <param name="categoryCode">分类代码</param>
        /// <param name="isMark">是否推荐</param>
        /// <param name="isAvailable">是否可用</param>
        /// <returns>访问地址列表</returns>
        [HttpGet("list")]
        [ProducesResponseType(typeof(PagedResponse<NetworkUrlListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNetworkUrlList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? name = null,
            [FromQuery] string? tagCodeTypes = null,
            [FromQuery] string? categoryCode = null,
            [FromQuery] long? isMark = null,
            [FromQuery] long? isAvailable = null)
        {
            try
            {
                var searchParams = new NetworkUrlSearchParams
                {
                    Name = name,
                    TagCodeTypes = string.IsNullOrEmpty(tagCodeTypes) ? null : tagCodeTypes.Split(',').ToList(),
                    CategoryCode = categoryCode,
                    IsMark = isMark,
                    IsAvailable = isAvailable
                };

                var networkUrls = await _networkUrlService.GetNetworkUrlList(page, pageSize, searchParams);
                var total = await _networkUrlService.GetNetworkUrlCount(searchParams);
                var totalPages = (int)Math.Ceiling((double)total / pageSize);

                var response = new PagedResponse<NetworkUrlListDto>
                {
                    Data = networkUrls,
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取访问地址列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据标签类型获取访问地址列表
        /// </summary>
        /// <param name="tagCodeType">标签类型</param>
        /// <returns>访问地址列表</returns>
        [HttpGet("by-tag-type/{tagCodeType}")]
        [ProducesResponseType(typeof(ApiResponse<NetworkUrlListDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNetworkUrlsByTagType(string tagCodeType)
        {
            try
            {
                var networkUrls = await _networkUrlService.GetNetworkUrlsByTagType(tagCodeType);
                return Ok(new ApiResponse<NetworkUrlListDto[]>
                {
                    Success = true,
                    Message = "获取成功",
                    Data = networkUrls.ToArray()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取访问地址列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取访问地址详情
        /// </summary>
        /// <param name="id">访问地址ID</param>
        /// <returns>访问地址详情</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<NetworkUrl>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetNetworkUrlById(int id)
        {
            try
            {
                var networkUrl = await _networkUrlService.GetNetworkUrlById(id);
                if (networkUrl == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "访问地址不存在" });
                }

                return Ok(new ApiResponse<NetworkUrl>
                {
                    Success = true,
                    Message = "获取成功",
                    Data = networkUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取访问地址详情失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建新访问地址记录
        /// </summary>
        /// <param name="networkUrlDto">访问地址创建信息</param>
        /// <returns>创建结果</returns>
        [HttpPost("create")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateNetworkUrl([FromBody] NetworkUrlDto networkUrlDto)
        {
            try
            {
                if (networkUrlDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToList();

                    var errorMessage = string.Join("; ", errors.SelectMany(e => e.Errors));
                    return BadRequest(new ApiErrorResponse
                    {
                        Message = "请求数据验证失败",
                        Details = errorMessage
                    });
                }

                var result = await _networkUrlService.CreateNetworkUrl(networkUrlDto);
                if (!result)
                {
                    return BadRequest(new ApiErrorResponse { Message = "URL地址已存在，请使用其他地址" });
                }

                return Ok(new ApiResponse { Message = "访问地址创建成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse
                {
                    Message = "创建访问地址失败",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// 更新访问地址
        /// </summary>
        /// <param name="id">访问地址ID</param>
        /// <param name="networkUrlDto">访问地址更新信息</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateNetworkUrl(int id, [FromBody] NetworkUrlDto networkUrlDto)
        {
            try
            {
                if (networkUrlDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }

                var result = await _networkUrlService.UpdateNetworkUrl(id, networkUrlDto);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "访问地址不存在或URL地址已被使用" });
                }

                return Ok(new ApiResponse { Message = "访问地址更新成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "更新访问地址失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除访问地址
        /// </summary>
        /// <param name="id">访问地址ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteNetworkUrl(int id)
        {
            try
            {
                var result = await _networkUrlService.DeleteNetworkUrl(id);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "访问地址不存在" });
                }

                return Ok(new ApiResponse { Message = "访问地址删除成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除访问地址失败", Details = ex.Message });
            }
        }
    }
}