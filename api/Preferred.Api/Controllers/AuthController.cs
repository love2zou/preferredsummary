using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 用户认证控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="userService">用户服务</param>
        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="userDto">用户注册信息</param>
        /// <returns>注册结果</returns>
        /// <remarks>
        /// 示例请求:
        /// 
        ///     POST /api/auth/register
        ///     {
        ///         "username": "john_doe",
        ///         "password": "123456",
        ///         "email": "john@example.com",
        ///         "firstName": "John",
        ///         "lastName": "Doe"
        ///     }
        /// 
        /// </remarks>
        /// <response code="200">注册成功</response>
        /// <response code="400">请求参数错误或用户已存在</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            if (string.IsNullOrEmpty(userDto.Username) || string.IsNullOrEmpty(userDto.Password))
            {
                return BadRequest(new ApiErrorResponse { Message = "用户名和密码不能为空" });
            }

            if (string.IsNullOrEmpty(userDto.Email))
            {
                return BadRequest(new ApiErrorResponse { Message = "邮箱不能为空" });
            }

            if (userDto.Password.Length < 6)
            {
                return BadRequest(new ApiErrorResponse { Message = "密码长度不能少于6位" });
            }

            var result = await _userService.Register(userDto);
            if (!result)
            {
                return BadRequest(new ApiErrorResponse { Message = "用户名或邮箱已存在" });
            }

            return Ok(new ApiResponse { Message = "注册成功" });
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="loginDto">用户登录信息</param>
        /// <returns>登录结果，包含JWT令牌和用户信息</returns>
        /// <remarks>
        /// 示例请求:
        /// 
        ///     POST /api/auth/login
        ///     {
        ///         "username": "john_doe",
        ///         "password": "123456"
        ///     }
        /// 
        /// </remarks>
        /// <response code="200">登录成功，返回JWT令牌和用户信息</response>
        /// <response code="400">用户名或密码错误</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            // 只有请求格式错误才返回 400
            if (string.IsNullOrEmpty(loginDto.UserName) || string.IsNullOrEmpty(loginDto.Password))
            {
                return BadRequest(new ApiErrorResponse { Message = "用户名和密码不能为空" });
            }
    
            var result = await _userService.Login(loginDto.UserName, loginDto.Password);
            
            // 所有业务逻辑结果都返回 200
            switch (result.ResultType)
            {
                case LoginResultType.Success:
                    return Ok(new ApiResponse<LoginResponseDto>
                    {
                        Success = true,
                        Message = "登录成功",
                        Data = result.Data
                    });
                case LoginResultType.UserNotFound:
                    return Ok(new ApiResponse<LoginResponseDto>
                    {
                        Success = false,
                        Message = "账号不存在",
                        ErrorCode = "USER_NOT_FOUND"
                    });
                case LoginResultType.PasswordIncorrect:
                    return Ok(new ApiResponse<LoginResponseDto>
                    {
                        Success = false,
                        Message = "密码错误",
                        ErrorCode = "PASSWORD_INCORRECT"
                    });
                case LoginResultType.UserDisabled:
                    return Ok(new ApiResponse<LoginResponseDto>
                    {
                        Success = false,
                        Message = "账号已被禁用，请联系管理员",
                        ErrorCode = "USER_DISABLED"
                    });
                default:
                    return Ok(new ApiResponse<LoginResponseDto>
                    {
                        Success = false,
                        Message = "登录失败",
                        ErrorCode = "LOGIN_FAILED"
                    });
            }
        }
    }
}