using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Preferred.Api.Models;
using Preferred.Api.Services;
using System;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 用户管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] // 需要JWT认证
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITagService _tagService;
        private readonly IPictureService _pictureService;

        public UserController(IUserService userService, ITagService tagService, IPictureService pictureService)
        {
            _userService = userService;
            _tagService = tagService;
            _pictureService = pictureService;
        }

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="size">每页数量，默认为10</param>
        /// <param name="username">用户名搜索</param>
        /// <param name="email">邮箱搜索</param>
        /// <param name="isActive">用户状态筛选</param>
        /// <returns>用户列表</returns>
        [HttpGet("list")]
        [ProducesResponseType(typeof(PagedResponse<UserListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserList(
            [FromQuery] int page = 1, 
            [FromQuery] int size = 10, 
            [FromQuery] string username = "",
            [FromQuery] string email = "",
            [FromQuery] bool? isActive = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (size < 1 || size > 100) size = 10;
                
                // 构建搜索参数对象
                var searchParams = new UserSearchParams
                {
                    UserName = username,
                    Email = email,
                    IsActive = isActive
                };
                
                var users = await _userService.GetAllUsers(page, size, searchParams);
                var total = await _userService.GetUsersCount(searchParams);
                var totalPages = (int)Math.Ceiling((double)total / size);
                
                var response = new PagedResponse<UserListDto>
                {
                    Data = users,
                    Total = total,
                    Page = page,
                    PageSize = size,
                    TotalPages = totalPages
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取用户列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取用户详情
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>用户详情</returns>
        [HttpGet("detail/{id}")]
        [ProducesResponseType(typeof(UserListDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserDetail(int id)
        {
            try
            {
                var userDto = await _userService.GetUserDetailDto(id);
                if (userDto == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在" });
                }
                return Ok(userDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取用户详情失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <param name="userDto">用户更新信息</param>
        /// <returns>更新结果</returns>
        [HttpPut("update/{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto userDto)
        {
            try
            {
                if (userDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }
                
                // 在更新用户信息之前，先获取旧的头像URL用于后续删除
                string oldProfilePictureUrl = null;
                if (!string.IsNullOrEmpty(userDto.ProfilePictureUrl))
                {
                    var existingUser = await _userService.GetUserById(id);
                    if (existingUser != null && !string.IsNullOrEmpty(existingUser.ProfilePictureUrl))
                    {
                        oldProfilePictureUrl = existingUser.ProfilePictureUrl;
                    }
                }
                
                var result = await _userService.UpdateUser(id, userDto);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在或邮箱已被使用" });
                }
                
                // 如果更新成功且有新头像，删除旧头像文件
                if (!string.IsNullOrEmpty(oldProfilePictureUrl) && 
                    !string.IsNullOrEmpty(userDto.ProfilePictureUrl) && 
                    oldProfilePictureUrl != userDto.ProfilePictureUrl)
                {
                    try
                    {
                        await _pictureService.DeleteImageFile(oldProfilePictureUrl);
                        Console.WriteLine($"已删除旧头像文件: {oldProfilePictureUrl}");
                    }
                    catch (Exception ex)
                    {
                        // 删除旧头像失败不影响主流程，只记录日志
                        Console.WriteLine($"删除旧头像文件失败: {ex.Message}");
                    }
                }
                
                return Ok(new ApiResponse { Message = "用户信息更新成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "更新用户信息失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("delete/{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var result = await _userService.DeleteUser(id);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在" });
                }
                
                return Ok(new ApiResponse { Message = "用户删除成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除用户失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 批量删除用户
        /// </summary>
        /// <param name="ids">用户ID数组</param>
        /// <returns>删除结果</returns>
        [HttpDelete("batch-delete")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BatchDeleteUsers([FromBody] int[] ids)
        {
            try
            {
                if (ids == null || ids.Length == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请选择要删除的用户" });
                }
                
                int deletedCount = 0;
                foreach (var id in ids)
                {
                    var result = await _userService.DeleteUser(id);
                    if (result) deletedCount++;
                }
                
                return Ok(new ApiResponse { Message = $"成功删除 {deletedCount} 个用户" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "批量删除用户失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 启用/禁用用户
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <param name="isActive">是否启用</param>
        /// <returns>操作结果</returns>
        [HttpPatch("toggle-status/{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleUserStatus(int id, [FromQuery] bool isActive)
        {
            try
            {
                var updateDto = new UserUpdateDto { IsActive = isActive };
                var result = await _userService.UpdateUser(id, updateDto);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在" });
                }
                
                string status = isActive ? "启用" : "禁用";
                return Ok(new ApiResponse { Message = $"用户{status}成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "操作失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建新用户
        /// </summary>
        /// <param name="userDto">用户创建信息</param>
        /// <returns>创建结果</returns>
        [HttpPost("create")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateUser([FromBody] UserDto userDto)
        {
            try
            {
                if (userDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }
                
                if (string.IsNullOrEmpty(userDto.Username) || string.IsNullOrEmpty(userDto.Password))
                {
                    return BadRequest(new ApiErrorResponse { Message = "用户名和密码不能为空" });
                }
                
                var result = await _userService.Register(userDto);
                if (!result)
                {
                    return BadRequest(new ApiErrorResponse { Message = "用户名或邮箱已存在" });
                }
                
                return Ok(new ApiResponse { Message = "用户创建成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建用户失败", Details = ex.Message });
            }
        }
         
        /// <summary>
        /// 修改用户密码
        /// </summary>
        /// <param name="changePasswordDto">修改密码信息</param>
        /// <returns>操作结果</returns>
        [HttpPost("change-password")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                if (changePasswordDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (string.IsNullOrEmpty(changePasswordDto.NewPassword))
                {
                    return BadRequest(new ApiErrorResponse { Message = "新密码不能为空" });
                }

                var result = await _userService.ChangePassword(changePasswordDto.UserId, changePasswordDto.NewPassword);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在或密码修改失败" });
                }

                return Ok(new ApiResponse { Message = "密码修改成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "修改密码失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 生成用户名和姓名对
        /// </summary>
        [HttpGet("generate-name-pair")]
        public async Task<ActionResult<ApiResponse<UserNamePair>>> GenerateUserNamePair()
        {
            try
            {
                var namePair = await _userService.GenerateUserNamePair();
                return Ok(new ApiResponse<UserNamePair> 
                { 
                    Data = namePair, 
                    Success = true,
                    Message = "用户名和姓名生成成功"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<UserNamePair> 
                { 
                    Success = false, 
                    Message = $"生成失败: {ex.Message}" 
                });
            }
        }
    }
}