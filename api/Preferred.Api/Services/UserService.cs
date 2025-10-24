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
                // 新增：注册时存储 FullName
                FullName = userDto.FullName,
                Email = userDto.Email ?? string.Empty,
                PasswordHash = passwordHash,
                Salt = salt,
                PhoneNumber = userDto.PhoneNumber,
                Bio = userDto.Bio,
                ProfilePictureUrl = userDto.ProfilePictureUrl,
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
            // 新增：按 FullName 模糊搜索
            if (!string.IsNullOrEmpty(searchParams.FullName))
            {
                query = query.Where(u => u.FullName.Contains(searchParams.FullName));
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
                    // 新增：列表返回 FullName
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Bio = u.Bio,
                    IsActive = u.IsActive,
                    IsEmailVerified = u.IsEmailVerified,
                    CrtTime = u.CrtTime,
                    LastLoginTime = u.LastLoginTime,
                    UserTypeCode = u.UserTypeCode,
                    UserToSystemCode = u.UserToSystemCode,
                    ProfilePictureUrl = u.ProfilePictureUrl
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
            // 新增：支持更新 FullName
            if (!string.IsNullOrEmpty(userDto.FullName))
                user.FullName = userDto.FullName;
            if (!string.IsNullOrEmpty(userDto.PhoneNumber))
                user.PhoneNumber = userDto.PhoneNumber;
            if (!string.IsNullOrEmpty(userDto.Bio))
                user.Bio = userDto.Bio;
            if (!string.IsNullOrEmpty(userDto.ProfilePictureUrl))
                user.ProfilePictureUrl = userDto.ProfilePictureUrl;
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
                // 新增：详情返回 FullName
                FullName = user.FullName,
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

        /// <summary>
        /// 生成用户名和姓名对
        /// </summary>
        public async Task<UserNamePair> GenerateUserNamePair()
    {
        // 词库定义
        var prefixes = new[] { "Power", "Strong", "Fit", "Elite", "Pro", "Max", "Ultra", "Prime", "Core", "Iron", "Steel", "Thunder", "Lightning", "Fire", "Storm" };
        var infixes = new[] { "Muscle", "Force", "Energy", "Vigor", "Strength", "Fitness", "Health", "Warrior", "Champion", "Master", "Hero", "Legend", "Titan", "Phoenix", "Dragon" };
        var suffixes = new[] { "Builder", "Trainer", "Coach", "Expert", "Guru", "Pro", "Star", "King", "Queen", "Lord", "Ace", "Elite", "Winner", "Leader", "Boss" };

        // 中文意译映射
        var chineseMapping = new Dictionary<string, string[]>
        {
            ["Power"] = new[] { "力量", "威力", "强力" },
            ["Strong"] = new[] { "强壮", "坚强", "刚强" },
            ["Fit"] = new[] { "健美", "适合", "精准" },
            ["Elite"] = new[] { "精英", "卓越", "顶尖" },
            ["Pro"] = new[] { "专业", "职业", "精通" },
            ["Max"] = new[] { "极限", "最大", "巅峰" },
            ["Ultra"] = new[] { "超越", "极致", "无限" },
            ["Prime"] = new[] { "黄金", "首要", "巅峰" },
            ["Core"] = new[] { "核心", "中坚", "精髓" },
            ["Iron"] = new[] { "钢铁", "坚韧", "不屈" },
            ["Steel"] = new[] { "钢铁", "坚硬", "锐利" },
            ["Thunder"] = new[] { "雷霆", "震撼", "威猛" },
            ["Lightning"] = new[] { "闪电", "迅捷", "敏锐" },
            ["Fire"] = new[] { "烈火", "激情", "燃烧" },
            ["Storm"] = new[] { "风暴", "狂野", "激烈" },
            ["Muscle"] = new[] { "肌肉", "力量", "强健" },
            ["Force"] = new[] { "力量", "威力", "冲击" },
            ["Energy"] = new[] { "能量", "活力", "动力" },
            ["Vigor"] = new[] { "活力", "精力", "朝气" },
            ["Strength"] = new[] { "力量", "强度", "坚韧" },
            ["Fitness"] = new[] { "健身", "体能", "健康" },
            ["Health"] = new[] { "健康", "康复", "养生" },
            ["Warrior"] = new[] { "战士", "勇士", "斗士" },
            ["Champion"] = new[] { "冠军", "胜者", "王者" },
            ["Master"] = new[] { "大师", "宗师", "专家" },
            ["Hero"] = new[] { "英雄", "豪杰", "勇者" },
            ["Legend"] = new[] { "传奇", "传说", "神话" },
            ["Titan"] = new[] { "泰坦", "巨人", "巨擘" },
            ["Phoenix"] = new[] { "凤凰", "不死鸟", "重生" },
            ["Dragon"] = new[] { "龙", "神龙", "飞龙" },
            ["Builder"] = new[] { "建造者", "塑造师", "构筑者" },
            ["Trainer"] = new[] { "训练师", "教练", "导师" },
            ["Coach"] = new[] { "教练", "指导者", "导师" },
            ["Expert"] = new[] { "专家", "行家", "能手" },
            ["Guru"] = new[] { "大师", "导师", "权威" },
            ["Star"] = new[] { "明星", "之星", "璀璨" },
            ["King"] = new[] { "王者", "君王", "霸主" },
            ["Queen"] = new[] { "女王", "王后", "皇后" },
            ["Lord"] = new[] { "领主", "主宰", "统领" },
            ["Ace"] = new[] { "王牌", "顶尖", "精英" },
            ["Winner"] = new[] { "胜者", "赢家", "成功者" },
            ["Leader"] = new[] { "领袖", "领导者", "先锋" },
            ["Boss"] = new[] { "老板", "首领", "主宰" }
        };

        var random = new Random();
        
        // 生成用户名和姓名（只生成一次）
        var prefix = prefixes[random.Next(prefixes.Length)];
        var infix = infixes[random.Next(infixes.Length)];
        var suffix = suffixes[random.Next(suffixes.Length)];
        
        // 生成4位随机流水号确保唯一性
        var serialNumber = random.Next(1000, 9999);
        
        // 生成英文用户名（驼峰命名法 + 4位流水号）
        var userName = $"{prefix}{infix}{suffix}{serialNumber}";
        
        // 生成中文姓名（概念意译）
        var prefixChinese = chineseMapping[prefix][random.Next(chineseMapping[prefix].Length)];
        var infixChinese = chineseMapping[infix][random.Next(chineseMapping[infix].Length)];
        var suffixChinese = chineseMapping[suffix][random.Next(chineseMapping[suffix].Length)];
        
        // 组合中文姓名，确保流畅性
        var fullName = CombineChineseName(prefixChinese, infixChinese, suffixChinese, random);

        return new UserNamePair
        {
            UserName = userName,
            FullName = fullName
        };
    }

    /// <summary>
    /// 组合中文姓名，确保流畅性和意境
    /// </summary>
    private string CombineChineseName(string prefix, string infix, string suffix, Random random)
    {
        // 根据不同组合模式生成中文姓名
        var patterns = new[]
        {
            $"{prefix}{infix}",           // 前缀+中缀
            $"{infix}{suffix}",           // 中缀+后缀  
            $"{prefix}{suffix}",          // 前缀+后缀
            $"{prefix}·{infix}",          // 前缀·中缀（带分隔符）
            $"{infix}·{suffix}",          // 中缀·后缀（带分隔符）
        };
        
        return patterns[random.Next(patterns.Length)];
    }
    }
}