using System.Threading.Tasks;
using System.Collections.Generic;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IUserService
    {
        Task<User> GetUserByUsername(string username);
        Task<bool> Register(UserDto userDto);
        // 修改登录方法返回类型
        Task<LoginResult> Login(string username, string password);
        
        // 用户管理接口
        Task<List<UserListDto>> GetAllUsers(int page = 1, int pageSize = 10, UserSearchParams searchParams = null);
        Task<int> GetUsersCount(UserSearchParams searchParams = null);
        Task<UserListDto> GetUserDetailDto(int id);
        Task<bool> UpdateUser(int id, UserUpdateDto userDto);
        Task<bool> DeleteUser(int id);
        Task<bool> ChangePassword(int userId, string newPassword);
    
        /// <summary>
        /// 生成用户名和姓名对
        /// </summary>
        /// <returns>包含英文用户名和中文姓名的对象</returns>
        Task<UserNamePair> GenerateUserNamePair();
}
}
