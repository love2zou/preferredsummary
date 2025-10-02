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

        public UserController(IUserService userService)
        {
            _userService = userService;
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
                var user = await _userService.GetUserById(id);
                if (user == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在" });
                }
                
                var userDto = new UserListDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    IsEmailVerified = user.IsEmailVerified,
                    CrtTime = user.CrtTime,
                    UpdTime = user.UpdTime,
                    LastLoginTime = user.LastLoginTime,
                    UserTypeCode = user.UserTypeCode,
                    UserToSystemCode = user.UserToSystemCode,
                    ProfilePictureUrl = user.ProfilePictureUrl // 新增：头像
                };
                
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
                
                var result = await _userService.UpdateUser(id, userDto);
                if (!result)
                {
                    return NotFound(new ApiErrorResponse { Message = "用户不存在或邮箱已被使用" });
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
         
    }
}