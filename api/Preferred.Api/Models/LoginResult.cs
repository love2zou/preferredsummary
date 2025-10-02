namespace Preferred.Api.Models
{
    /// <summary>
    /// 登录结果枚举
    /// </summary>
    public enum LoginResultType
    {
        /// <summary>
        /// 登录成功
        /// </summary>
        Success,
        /// <summary>
        /// 用户不存在
        /// </summary>
        UserNotFound,
        /// <summary>
        /// 密码错误
        /// </summary>
        PasswordIncorrect,
        /// <summary>
        /// 用户已被禁用
        /// </summary>
        UserDisabled
    }

    /// <summary>
    /// 登录结果模型
    /// </summary>
    public class LoginResult
    {
        public LoginResultType ResultType { get; set; }
        public LoginResponseDto Data { get; set; }
        public string Message { get; set; }
    }
}