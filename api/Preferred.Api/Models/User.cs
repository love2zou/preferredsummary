using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Preferred.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? Bio { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
        public DateTime? LastLoginTime { get; set; }
        // 新增字段（重命名）
        public string? UserTypeCode { get; set; }
        public string? UserToSystemCode { get; set; }
        public int SeqNo { get; set; }
    }

    /// <summary>
    /// 用户注册请求模型
    /// </summary>
    public class UserDto
    {
        /// <summary>
        /// 用户名
        /// </summary>
        /// <example>john_doe</example>
        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "用户名长度必须在3-50个字符之间")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        /// <example>123456</example>
        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6-100个字符之间")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱地址（注册时必填）
        /// </summary>
        /// <example>john@example.com</example>
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        [StringLength(100, ErrorMessage = "邮箱长度不能超过100个字符")]
        public string? Email { get; set; }
        
        /// <summary>
        /// 电话号码
        /// </summary>
        /// <example>13800138000</example>
        [StringLength(20, ErrorMessage = "电话号码长度不能超过20个字符")]
        public string? PhoneNumber { get; set; }
        
        /// <summary>
        /// 个人简介
        /// </summary>
        /// <example>这是我的个人简介</example>
        [StringLength(500, ErrorMessage = "个人简介长度不能超过500个字符")]
        public string? Bio { get; set; }
        // 新增：用户类型与所属系统
        [StringLength(50, ErrorMessage = "用户类型长度不能超过50个字符")]
        public string? UserTypeCode { get; set; }
        [StringLength(100, ErrorMessage = "所属系统长度不能超过100个字符")]
        public string? UserToSystemCode { get; set; }
        /// <summary>
        /// 头像URL
        /// </summary>
        /// <example>https://example.com/avatar.jpg</example>
        [StringLength(200, ErrorMessage = "头像URL长度不能超过200个字符")]
        public string? ProfilePictureUrl { get; set; }
    }

    /// <summary>
    /// 用户登录请求模型
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// 用户名
        /// </summary>
        /// <example>john_doe</example>
        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "用户名长度必须在3-50个字符之间")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        /// <example>123456</example>
        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6-100个字符之间")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 登录成功响应模型
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>
        /// JWT访问令牌
        /// </summary>
        /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
        public string Token { get; set; } = string.Empty;
        /// <summary>
        /// 用户Id
        /// </summary>
        /// <example>1</example>
        public int UserId { get; set; }
        /// <summary>
        /// 用户名
        /// </summary>
        /// <example>john_doe</example>
        public string UserName { get; set; } = string.Empty;
        /// <summary>
        /// 用户类型
        /// </summary>
        /// <example>jiaolian</example>
        public string UserTypeCode { get; set; } = string.Empty;
        /// <summary>
        /// 邮箱地址
        /// </summary>
        /// <example>john@example.com</example>
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户列表响应模型
    /// </summary>
    public class UserListDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
        public DateTime? LastLoginTime { get; set; }
        // 重命名：用户类型与所属系统代码
        public string? UserTypeCode { get; set; }
        public string? UserToSystemCode { get; set; }
        public string? ProfilePictureUrl { get; set; }
        // 新增：从 tb_tag 联动的用户类型信息
        public string? UserTypeName { get; set; }
        public string? UserTypeHexColor { get; set; }
        public string? UserTypeRgbColor { get; set; }
    }

    /// <summary>
    /// 用户更新请求模型
    /// </summary>
    public class UserUpdateDto
    {
        [StringLength(100, ErrorMessage = "邮箱长度不能超过100个字符")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string? Email { get; set; }
        
        [StringLength(20, ErrorMessage = "电话号码长度不能超过20个字符")]
        public string? PhoneNumber { get; set; }
        
        [StringLength(500, ErrorMessage = "个人简介长度不能超过500个字符")]
        public string? Bio { get; set; }
        
        public bool? IsActive { get; set; }
        
        public bool? IsEmailVerified { get; set; }
        [StringLength(50, ErrorMessage = "用户类型长度不能超过50个字符")]
        public string UserTypeCode { get; set; }
    
        [StringLength(100, ErrorMessage = "所属系统长度不能超过100个字符")]
        public string UserToSystemCode { get; set; }
        [StringLength(200, ErrorMessage = "头像地址长度不能超过200个字符")]
        public string? ProfilePictureUrl { get; set; }
    }
    
    /// <summary>
    /// 用户搜索参数
    /// </summary>
    public class UserSearchParams
    {
        /// <summary>
        /// 用户名搜索
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// 邮箱搜索
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 用户状态筛选
        /// </summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// 修改密码请求模型
    /// </summary>
    public class ChangePasswordDto
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        [Required(ErrorMessage = "用户ID不能为空")]
        public int UserId { get; set; }

        /// <summary>
        /// 新密码
        /// </summary>
        [Required(ErrorMessage = "新密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6-100个字符之间")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
