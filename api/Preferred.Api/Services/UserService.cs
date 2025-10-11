using System;
using System.Linq;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Models;
using Preferred.Api.Data;

namespace Preferred.Api.Services
{
    public class UserService : IUserService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public UserService(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public async Task<User> GetUserByUsername(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
        }

        // 注册
        // 方法：Register(UserDto userDto)
        public async Task<bool> Register(UserDto userDto)
        {
            if (await _context.Users.AnyAsync(u => u.UserName == userDto.Username))
            {
                return false;
            }
            // 新增：手机号唯一校验（不为空时才校验）
            if (!string.IsNullOrWhiteSpace(userDto.PhoneNumber) &&
                await _context.Users.AnyAsync(u => u.PhoneNumber == userDto.PhoneNumber))
            {
                return false;
            }

            var salt = GenerateSalt();
            var passwordHash = HashPassword(userDto.Password, salt);

            var user = new User
            {
                UserName = userDto.Username,
                Email = userDto.Email ?? string.Empty,
                PasswordHash = passwordHash,
                Salt = salt,
                PhoneNumber = userDto.PhoneNumber,
                Bio = userDto.Bio,
                ProfilePictureUrl = userDto.ProfilePictureUrl, // 新增：头像
                CrtTime = DateTime.UtcNow,
                UpdTime = DateTime.UtcNow,
                UserTypeCode = userDto.UserTypeCode,
                UserToSystemCode = userDto.UserToSystemCode,
                SeqNo = 0
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return true;
        }

        // 登录：支持用户名或手机号
        public async Task<LoginResult> Login(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == username || u.PhoneNumber == username);
            if (user == null)
            {
                return new LoginResult { ResultType = LoginResultType.UserNotFound, Message = "账号不存在" };
            }
            if (!user.IsActive)
            {
                return new LoginResult { ResultType = LoginResultType.UserDisabled, Message = "账号已被禁用，请联系管理员" };
            }
        
            var passwordHash = HashPassword(password, user.Salt);
            if (passwordHash != user.PasswordHash)
            {
                return new LoginResult { ResultType = LoginResultType.PasswordIncorrect, Message = "密码错误" };
            }
        
            user.LastLoginTime = DateTime.UtcNow;
            user.UpdTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        
            var token = GenerateJwtToken(user);
            return new LoginResult
            {
                ResultType = LoginResultType.Success,
                Data = new LoginResponseDto
                {
                    Token = token,
                    UserId = user.Id,
                    UserName = user.UserName,
                    UserTypeCode = user.UserTypeCode ?? string.Empty,
                    Email = user.Email ?? string.Empty
                },
                Message = "登录成功"
            };
        }
        
        private string GenerateSalt()
        {
            var bytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password + salt);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email ?? "")
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<List<UserListDto>> GetAllUsers(int page = 1, int pageSize = 10, UserSearchParams searchParams = null)
        {
            var query = _context.Users.AsQueryable();

            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.UserName))
                {
                    query = query.Where(u => u.UserName.Contains(searchParams.UserName));
                }

                if (!string.IsNullOrEmpty(searchParams.Email))
                {
                    query = query.Where(u => u.Email.Contains(searchParams.Email));
                }

                if (searchParams.IsActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == searchParams.IsActive.Value);
                }
            }

            var users = await query
                .OrderByDescending(u => u.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Bio = u.Bio,
                    IsActive = u.IsActive,
                    IsEmailVerified = u.IsEmailVerified,
                    CrtTime = u.CrtTime,
                    LastLoginTime = u.LastLoginTime,
                    UserTypeCode = u.UserTypeCode,
                    UserToSystemCode = u.UserToSystemCode,
                    ProfilePictureUrl = u.ProfilePictureUrl // 新增：头像
                })
                .ToListAsync();
            return users;
        }
        
        public async Task<int> GetUsersCount(UserSearchParams searchParams = null)
        {
            var query = _context.Users.AsQueryable();

            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.UserName))
                {
                    query = query.Where(u => u.UserName.Contains(searchParams.UserName));
                }

                if (!string.IsNullOrEmpty(searchParams.Email))
                {
                    query = query.Where(u => u.Email.Contains(searchParams.Email));
                }

                if (searchParams.IsActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == searchParams.IsActive.Value);
                }
            }

            return await query.CountAsync();
        }
        
        public async Task<User> GetUserById(int id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }
        
        public async Task<bool> UpdateUser(int id, UserUpdateDto userDto)
        {
            var user = await GetUserById(id);
            if (user == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(userDto.Email) && userDto.Email != user.Email)
            {
                if (await _context.Users.AnyAsync(u => u.Email == userDto.Email && u.Id != id))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(userDto.Email))
                user.Email = userDto.Email;
            if (!string.IsNullOrEmpty(userDto.PhoneNumber))
                user.PhoneNumber = userDto.PhoneNumber;
            if (!string.IsNullOrEmpty(userDto.Bio))
                user.Bio = userDto.Bio;
            if (!string.IsNullOrEmpty(userDto.ProfilePictureUrl))
                user.ProfilePictureUrl = userDto.ProfilePictureUrl; // 新增：头像
            if (!string.IsNullOrEmpty(userDto.UserTypeCode))
                user.UserTypeCode = userDto.UserTypeCode;
            if (!string.IsNullOrEmpty(userDto.UserToSystemCode))
                user.UserToSystemCode = userDto.UserToSystemCode;
            user.UpdTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
        
        public async Task<bool> DeleteUser(int id)
        {
            var user = await GetUserById(id);
            if (user == null)
            {
                return false;
            }
            
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePassword(int userId, string newPassword)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var salt = GenerateSalt();
                var passwordHash = HashPassword(newPassword, salt);

                user.PasswordHash = passwordHash;
                user.Salt = salt;
                user.UpdTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<UserListDto> GetUserDetailDto(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return null;
            }

            var dto = new UserListDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified,
                CrtTime = user.CrtTime,
                UpdTime = user.UpdTime,
                LastLoginTime = user.LastLoginTime,
                UserTypeCode = user.UserTypeCode,
                UserToSystemCode = user.UserToSystemCode,
                ProfilePictureUrl = user.ProfilePictureUrl
            };

            if (!string.IsNullOrWhiteSpace(user.UserTypeCode))
            {
                var tag = await _context.Tags
                    .Where(t => t.TagCode == user.UserTypeCode)
                    .OrderBy(t => t.SeqNo)
                    .FirstOrDefaultAsync();

                if (tag != null)
                {
                    dto.UserTypeName = tag.TagName;
                    dto.UserTypeHexColor = tag.HexColor;
                    dto.UserTypeRgbColor = tag.RgbColor;
                }
            }

            return dto;
        }
    }
}