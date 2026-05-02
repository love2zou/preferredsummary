using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public class ReservationAdminService : IReservationAdminService
    {
        private readonly ApplicationDbContext _context;

        public ReservationAdminService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReservationLookupOptionDto>> GetClubOptionsAsync()
        {
            return await _context.ReservationClubs
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .Select(item => new ReservationLookupOptionDto
                {
                    Id = item.Id,
                    Label = item.ClubName,
                    Description = item.City
                })
                .ToListAsync();
        }

        public async Task<List<ReservationLookupOptionDto>> GetTrainerOptionsAsync()
        {
            var users = await _context.Users.AsNoTracking().ToDictionaryAsync(item => item.Id);
            var clubs = await _context.ReservationClubs.AsNoTracking().ToDictionaryAsync(item => item.Id);

            return await _context.ReservationTrainerProfiles
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .Select(item => new ReservationLookupOptionDto
                {
                    Id = item.Id,
                    Label = item.DisplayName,
                    Description = string.Empty
                })
                .ToListAsync();
        }

        public async Task<List<ReservationLookupOptionDto>> GetUserOptionsAsync(string? keyword = null)
        {
            var query = _context.Users
                .AsNoTracking()
                .Where(item => item.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var search = keyword.Trim();
                query = query.Where(item =>
                    item.UserName.Contains(search) ||
                    (item.FullName != null && item.FullName.Contains(search)) ||
                    (item.PhoneNumber != null && item.PhoneNumber.Contains(search)));
            }

            return await query
                .OrderByDescending(item => item.LastLoginTime)
                .ThenBy(item => item.Id)
                .Take(100)
                .Select(item => new ReservationLookupOptionDto
                {
                    Id = item.Id,
                    Label = string.IsNullOrWhiteSpace(item.FullName) ? item.UserName : $"{item.FullName} ({item.UserName})",
                    Description = item.PhoneNumber
                })
                .ToListAsync();
        }

        public async Task<List<ReservationClubAdminDto>> GetClubListAsync(int page, int pageSize, ReservationClubAdminSearchParams? searchParams = null)
        {
            var query = ApplyClubSearch(_context.ReservationClubs.AsNoTracking(), searchParams);

            return await query
                .OrderBy(item => item.SeqNo)
                .ThenByDescending(item => item.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new ReservationClubAdminDto
                {
                    Id = item.Id,
                    ClubCode = item.ClubCode,
                    ClubName = item.ClubName,
                    City = item.City,
                    District = item.District,
                    Address = item.Address,
                    BusinessHours = item.BusinessHours,
                    IsActive = item.IsActive,
                    SeqNo = item.SeqNo,
                    CrtTime = item.CrtTime,
                    UpdTime = item.UpdTime
                })
                .ToListAsync();
        }

        public async Task<int> GetClubCountAsync(ReservationClubAdminSearchParams? searchParams = null)
        {
            return await ApplyClubSearch(_context.ReservationClubs.AsNoTracking(), searchParams).CountAsync();
        }

        public async Task<ReservationClubAdminDto?> GetClubByIdAsync(int id)
        {
            return await _context.ReservationClubs
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new ReservationClubAdminDto
                {
                    Id = item.Id,
                    ClubCode = item.ClubCode,
                    ClubName = item.ClubName,
                    City = item.City,
                    District = item.District,
                    Address = item.Address,
                    BusinessHours = item.BusinessHours,
                    IsActive = item.IsActive,
                    SeqNo = item.SeqNo,
                    CrtTime = item.CrtTime,
                    UpdTime = item.UpdTime
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> CreateClubAsync(ReservationClubAdminEditDto dto)
        {
            if (await _context.ReservationClubs.AnyAsync(item => item.ClubCode == dto.ClubCode))
            {
                return false;
            }

            var now = DateTime.Now;
            _context.ReservationClubs.Add(new ReservationClub
            {
                ClubCode = dto.ClubCode.Trim(),
                ClubName = dto.ClubName.Trim(),
                City = dto.City.Trim(),
                District = dto.District?.Trim(),
                Address = dto.Address?.Trim(),
                BusinessHours = dto.BusinessHours?.Trim(),
                IsActive = dto.IsActive,
                SeqNo = dto.SeqNo,
                CrtTime = now,
                UpdTime = now
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateClubAsync(int id, ReservationClubAdminEditDto dto)
        {
            var club = await _context.ReservationClubs.FindAsync(id);
            if (club == null)
            {
                return false;
            }

            var duplicated = await _context.ReservationClubs.AnyAsync(item => item.ClubCode == dto.ClubCode && item.Id != id);
            if (duplicated)
            {
                return false;
            }

            club.ClubCode = dto.ClubCode.Trim();
            club.ClubName = dto.ClubName.Trim();
            club.City = dto.City.Trim();
            club.District = dto.District?.Trim();
            club.Address = dto.Address?.Trim();
            club.BusinessHours = dto.BusinessHours?.Trim();
            club.IsActive = dto.IsActive;
            club.SeqNo = dto.SeqNo;
            club.UpdTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteClubAsync(int id)
        {
            var club = await _context.ReservationClubs.FindAsync(id);
            if (club == null)
            {
                return false;
            }

            var used = await _context.ReservationTrainerProfiles.AnyAsync(item => item.ClubId == id)
                || await _context.ReservationMemberPackages.AnyAsync(item => item.ClubId == id)
                || await _context.ReservationOrders.AnyAsync(item => item.ClubId == id);
            if (used)
            {
                return false;
            }

            _context.ReservationClubs.Remove(club);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ReservationTrainerAdminDto>> GetTrainerListAsync(int page, int pageSize, ReservationTrainerAdminSearchParams? searchParams = null)
        {
            var query = ApplyTrainerSearch(_context.ReservationTrainerProfiles.AsNoTracking(), searchParams);
            var profiles = await query
                .OrderByDescending(item => item.IsRecommended)
                .ThenBy(item => item.SeqNo)
                .ThenByDescending(item => item.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return await BuildTrainerAdminDtosAsync(profiles);
        }

        public async Task<int> GetTrainerCountAsync(ReservationTrainerAdminSearchParams? searchParams = null)
        {
            return await ApplyTrainerSearch(_context.ReservationTrainerProfiles.AsNoTracking(), searchParams).CountAsync();
        }

        public async Task<ReservationTrainerAdminDto?> GetTrainerByIdAsync(int id)
        {
            var profile = await _context.ReservationTrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
            if (profile == null)
            {
                return null;
            }

            return (await BuildTrainerAdminDtosAsync(new List<ReservationTrainerProfile> { profile })).FirstOrDefault();
        }

        public async Task<bool> CreateTrainerAsync(ReservationTrainerAdminEditDto dto)
        {
            if (await _context.ReservationTrainerProfiles.AnyAsync(item => item.UserId == dto.UserId))
            {
                return false;
            }

            var now = DateTime.Now;
            var profile = new ReservationTrainerProfile
            {
                UserId = dto.UserId,
                ClubId = dto.ClubId,
                DisplayName = dto.DisplayName.Trim(),
                Title = dto.Title.Trim(),
                Gender = dto.Gender.Trim(),
                YearsOfExperience = dto.YearsOfExperience,
                Rating = dto.Rating,
                ReviewCount = dto.ReviewCount,
                ServedClients = dto.ServedClients,
                Satisfaction = dto.Satisfaction,
                BasePrice = dto.BasePrice,
                TrainingArea = dto.TrainingArea?.Trim(),
                Highlight = dto.Highlight?.Trim(),
                Introduction = dto.Introduction?.Trim(),
                Story = dto.Story?.Trim(),
                HeroImageUrl = dto.HeroImageUrl?.Trim(),
                HeroTone = dto.HeroTone?.Trim(),
                AccentTone = dto.AccentTone?.Trim(),
                IsRecommended = dto.IsRecommended,
                IsActive = dto.IsActive,
                SeqNo = dto.SeqNo,
                CrtTime = now,
                UpdTime = now
            };

            _context.ReservationTrainerProfiles.Add(profile);
            await _context.SaveChangesAsync();
            await ReplaceTrainerTagsAsync(profile.Id, dto, now);
            return true;
        }

        public async Task<bool> UpdateTrainerAsync(int id, ReservationTrainerAdminEditDto dto)
        {
            var profile = await _context.ReservationTrainerProfiles.FindAsync(id);
            if (profile == null)
            {
                return false;
            }

            var duplicated = await _context.ReservationTrainerProfiles.AnyAsync(item => item.UserId == dto.UserId && item.Id != id);
            if (duplicated)
            {
                return false;
            }

            profile.UserId = dto.UserId;
            profile.ClubId = dto.ClubId;
            profile.DisplayName = dto.DisplayName.Trim();
            profile.Title = dto.Title.Trim();
            profile.Gender = dto.Gender.Trim();
            profile.YearsOfExperience = dto.YearsOfExperience;
            profile.Rating = dto.Rating;
            profile.ReviewCount = dto.ReviewCount;
            profile.ServedClients = dto.ServedClients;
            profile.Satisfaction = dto.Satisfaction;
            profile.BasePrice = dto.BasePrice;
            profile.TrainingArea = dto.TrainingArea?.Trim();
            profile.Highlight = dto.Highlight?.Trim();
            profile.Introduction = dto.Introduction?.Trim();
            profile.Story = dto.Story?.Trim();
            profile.HeroImageUrl = dto.HeroImageUrl?.Trim();
            profile.HeroTone = dto.HeroTone?.Trim();
            profile.AccentTone = dto.AccentTone?.Trim();
            profile.IsRecommended = dto.IsRecommended;
            profile.IsActive = dto.IsActive;
            profile.SeqNo = dto.SeqNo;
            profile.UpdTime = DateTime.Now;

            await _context.SaveChangesAsync();
            await ReplaceTrainerTagsAsync(profile.Id, dto, DateTime.Now);
            return true;
        }

        public async Task<bool> DeleteTrainerAsync(int id)
        {
            var profile = await _context.ReservationTrainerProfiles.FindAsync(id);
            if (profile == null)
            {
                return false;
            }

            var used = await _context.ReservationOrders.AnyAsync(item => item.TrainerProfileId == id);
            if (used)
            {
                return false;
            }

            var tags = await _context.ReservationTrainerTags.Where(item => item.TrainerProfileId == id).ToListAsync();
            var sessions = await _context.ReservationTrainerSessionTypes.Where(item => item.TrainerProfileId == id).ToListAsync();
            var schedules = await _context.ReservationTrainerScheduleSlots.Where(item => item.TrainerProfileId == id).ToListAsync();

            _context.ReservationTrainerTags.RemoveRange(tags);
            _context.ReservationTrainerSessionTypes.RemoveRange(sessions);
            _context.ReservationTrainerScheduleSlots.RemoveRange(schedules);
            _context.ReservationTrainerProfiles.Remove(profile);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ReservationSessionAdminDto>> GetSessionListAsync(int page, int pageSize, ReservationSessionAdminSearchParams? searchParams = null)
        {
            var query = ApplySessionSearch(_context.ReservationTrainerSessionTypes.AsNoTracking(), searchParams);
            var sessions = await query
                .OrderBy(item => item.SeqNo)
                .ThenByDescending(item => item.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var trainerMap = await _context.ReservationTrainerProfiles.AsNoTracking()
                .Where(item => sessions.Select(x => x.TrainerProfileId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName);

            return sessions.Select(item => new ReservationSessionAdminDto
            {
                Id = item.Id,
                TrainerProfileId = item.TrainerProfileId,
                TrainerName = trainerMap.TryGetValue(item.TrainerProfileId, out var trainerName) ? trainerName : string.Empty,
                SessionCode = item.SessionCode,
                SessionName = item.SessionName,
                Description = item.Description,
                DurationMinutes = item.DurationMinutes,
                Price = item.Price,
                IsActive = item.IsActive,
                SeqNo = item.SeqNo,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            }).ToList();
        }

        public async Task<int> GetSessionCountAsync(ReservationSessionAdminSearchParams? searchParams = null)
        {
            return await ApplySessionSearch(_context.ReservationTrainerSessionTypes.AsNoTracking(), searchParams).CountAsync();
        }

        public async Task<ReservationSessionAdminDto?> GetSessionByIdAsync(int id)
        {
            var item = await _context.ReservationTrainerSessionTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                return null;
            }

            var trainerName = await _context.ReservationTrainerProfiles.AsNoTracking()
                .Where(x => x.Id == item.TrainerProfileId)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync();

            return new ReservationSessionAdminDto
            {
                Id = item.Id,
                TrainerProfileId = item.TrainerProfileId,
                TrainerName = trainerName ?? string.Empty,
                SessionCode = item.SessionCode,
                SessionName = item.SessionName,
                Description = item.Description,
                DurationMinutes = item.DurationMinutes,
                Price = item.Price,
                IsActive = item.IsActive,
                SeqNo = item.SeqNo,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            };
        }

        public async Task<bool> CreateSessionAsync(ReservationSessionAdminEditDto dto)
        {
            var duplicated = await _context.ReservationTrainerSessionTypes.AnyAsync(item =>
                item.TrainerProfileId == dto.TrainerProfileId && item.SessionCode == dto.SessionCode);
            if (duplicated)
            {
                return false;
            }

            var now = DateTime.Now;
            _context.ReservationTrainerSessionTypes.Add(new ReservationTrainerSessionType
            {
                TrainerProfileId = dto.TrainerProfileId,
                SessionCode = dto.SessionCode.Trim(),
                SessionName = dto.SessionName.Trim(),
                Description = dto.Description?.Trim(),
                DurationMinutes = dto.DurationMinutes,
                Price = dto.Price,
                IsActive = dto.IsActive,
                SeqNo = dto.SeqNo,
                CrtTime = now,
                UpdTime = now
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateSessionAsync(int id, ReservationSessionAdminEditDto dto)
        {
            var item = await _context.ReservationTrainerSessionTypes.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            var duplicated = await _context.ReservationTrainerSessionTypes.AnyAsync(x =>
                x.TrainerProfileId == dto.TrainerProfileId && x.SessionCode == dto.SessionCode && x.Id != id);
            if (duplicated)
            {
                return false;
            }

            item.TrainerProfileId = dto.TrainerProfileId;
            item.SessionCode = dto.SessionCode.Trim();
            item.SessionName = dto.SessionName.Trim();
            item.Description = dto.Description?.Trim();
            item.DurationMinutes = dto.DurationMinutes;
            item.Price = dto.Price;
            item.IsActive = dto.IsActive;
            item.SeqNo = dto.SeqNo;
            item.UpdTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteSessionAsync(int id)
        {
            var item = await _context.ReservationTrainerSessionTypes.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            var used = await _context.ReservationOrders.AnyAsync(x => x.SessionTypeId == id);
            if (used)
            {
                return false;
            }

            _context.ReservationTrainerSessionTypes.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ReservationScheduleAdminDto>> GetScheduleListAsync(int page, int pageSize, ReservationScheduleAdminSearchParams? searchParams = null)
        {
            var query = ApplyScheduleSearch(_context.ReservationTrainerScheduleSlots.AsNoTracking(), searchParams);
            var schedules = await query
                .OrderByDescending(item => item.ScheduleDate)
                .ThenBy(item => item.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var trainerMap = await _context.ReservationTrainerProfiles.AsNoTracking()
                .Where(item => schedules.Select(x => x.TrainerProfileId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName);
            var clubMap = await _context.ReservationClubs.AsNoTracking()
                .Where(item => schedules.Select(x => x.ClubId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.ClubName);

            return schedules.Select(item => new ReservationScheduleAdminDto
            {
                Id = item.Id,
                TrainerProfileId = item.TrainerProfileId,
                TrainerName = trainerMap.TryGetValue(item.TrainerProfileId, out var trainerName) ? trainerName : string.Empty,
                ClubId = item.ClubId,
                ClubName = clubMap.TryGetValue(item.ClubId, out var clubName) ? clubName : string.Empty,
                ScheduleDate = item.ScheduleDate.ToString("yyyy-MM-dd"),
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                IsAvailable = item.IsAvailable,
                SeqNo = item.SeqNo,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            }).ToList();
        }

        public async Task<int> GetScheduleCountAsync(ReservationScheduleAdminSearchParams? searchParams = null)
        {
            return await ApplyScheduleSearch(_context.ReservationTrainerScheduleSlots.AsNoTracking(), searchParams).CountAsync();
        }

        public async Task<ReservationScheduleAdminDto?> GetScheduleByIdAsync(int id)
        {
            var item = await _context.ReservationTrainerScheduleSlots.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                return null;
            }

            var trainerName = await _context.ReservationTrainerProfiles.AsNoTracking()
                .Where(x => x.Id == item.TrainerProfileId)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync();
            var clubName = await _context.ReservationClubs.AsNoTracking()
                .Where(x => x.Id == item.ClubId)
                .Select(x => x.ClubName)
                .FirstOrDefaultAsync();

            return new ReservationScheduleAdminDto
            {
                Id = item.Id,
                TrainerProfileId = item.TrainerProfileId,
                TrainerName = trainerName ?? string.Empty,
                ClubId = item.ClubId,
                ClubName = clubName ?? string.Empty,
                ScheduleDate = item.ScheduleDate.ToString("yyyy-MM-dd"),
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                IsAvailable = item.IsAvailable,
                SeqNo = item.SeqNo,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            };
        }

        public async Task<bool> CreateScheduleAsync(ReservationScheduleAdminEditDto dto)
        {
            if (!TryParseDate(dto.ScheduleDate, out var scheduleDate))
            {
                return false;
            }

            var duplicated = await _context.ReservationTrainerScheduleSlots.AnyAsync(item =>
                item.TrainerProfileId == dto.TrainerProfileId &&
                item.ScheduleDate == scheduleDate &&
                item.StartTime == dto.StartTime &&
                item.EndTime == dto.EndTime);
            if (duplicated)
            {
                return false;
            }

            var now = DateTime.Now;
            _context.ReservationTrainerScheduleSlots.Add(new ReservationTrainerScheduleSlot
            {
                TrainerProfileId = dto.TrainerProfileId,
                ClubId = dto.ClubId,
                ScheduleDate = scheduleDate,
                StartTime = dto.StartTime.Trim(),
                EndTime = dto.EndTime.Trim(),
                IsAvailable = dto.IsAvailable,
                SeqNo = dto.SeqNo,
                CrtTime = now,
                UpdTime = now
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateScheduleAsync(int id, ReservationScheduleAdminEditDto dto)
        {
            var item = await _context.ReservationTrainerScheduleSlots.FindAsync(id);
            if (item == null || !TryParseDate(dto.ScheduleDate, out var scheduleDate))
            {
                return false;
            }

            var duplicated = await _context.ReservationTrainerScheduleSlots.AnyAsync(x =>
                x.TrainerProfileId == dto.TrainerProfileId &&
                x.ScheduleDate == scheduleDate &&
                x.StartTime == dto.StartTime &&
                x.EndTime == dto.EndTime &&
                x.Id != id);
            if (duplicated)
            {
                return false;
            }

            item.TrainerProfileId = dto.TrainerProfileId;
            item.ClubId = dto.ClubId;
            item.ScheduleDate = scheduleDate;
            item.StartTime = dto.StartTime.Trim();
            item.EndTime = dto.EndTime.Trim();
            item.IsAvailable = dto.IsAvailable;
            item.SeqNo = dto.SeqNo;
            item.UpdTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteScheduleAsync(int id)
        {
            var item = await _context.ReservationTrainerScheduleSlots.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            var used = await _context.ReservationOrders.AnyAsync(x => x.ScheduleSlotId == id && x.StatusCode != "Cancelled");
            if (used)
            {
                return false;
            }

            _context.ReservationTrainerScheduleSlots.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ReservationPackageAdminDto>> GetPackageListAsync(int page, int pageSize, ReservationPackageAdminSearchParams? searchParams = null)
        {
            var query = ApplyPackageSearch(_context.ReservationMemberPackages.AsNoTracking(), searchParams);
            var packages = await query
                .OrderByDescending(item => item.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var clubMap = await _context.ReservationClubs.AsNoTracking()
                .Where(item => packages.Select(x => x.ClubId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.ClubName);
            var userMap = await _context.Users.AsNoTracking()
                .Where(item => packages.Select(x => x.MemberId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            return packages.Select(item => new ReservationPackageAdminDto
            {
                Id = item.Id,
                MemberId = item.MemberId,
                MemberName = userMap.TryGetValue(item.MemberId, out var user) ? ResolveUserName(user) : string.Empty,
                ClubId = item.ClubId,
                ClubName = clubMap.TryGetValue(item.ClubId, out var clubName) ? clubName : string.Empty,
                PackageName = item.PackageName,
                MembershipName = item.MembershipName,
                TotalSessions = item.TotalSessions,
                RemainingSessions = item.RemainingSessions,
                EffectiveDate = item.EffectiveDate.ToString("yyyy-MM-dd"),
                ExpireDate = item.ExpireDate.ToString("yyyy-MM-dd"),
                StatusCode = item.StatusCode,
                SeqNo = item.SeqNo,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            }).ToList();
        }

        public async Task<int> GetPackageCountAsync(ReservationPackageAdminSearchParams? searchParams = null)
        {
            return await ApplyPackageSearch(_context.ReservationMemberPackages.AsNoTracking(), searchParams).CountAsync();
        }

        public async Task<ReservationPackageAdminDto?> GetPackageByIdAsync(int id)
        {
            var item = await _context.ReservationMemberPackages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                return null;
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.MemberId);
            var clubName = await _context.ReservationClubs.AsNoTracking()
                .Where(x => x.Id == item.ClubId)
                .Select(x => x.ClubName)
                .FirstOrDefaultAsync();

            return new ReservationPackageAdminDto
            {
                Id = item.Id,
                MemberId = item.MemberId,
                MemberName = user == null ? string.Empty : ResolveUserName(user),
                ClubId = item.ClubId,
                ClubName = clubName ?? string.Empty,
                PackageName = item.PackageName,
                MembershipName = item.MembershipName,
                TotalSessions = item.TotalSessions,
                RemainingSessions = item.RemainingSessions,
                EffectiveDate = item.EffectiveDate.ToString("yyyy-MM-dd"),
                ExpireDate = item.ExpireDate.ToString("yyyy-MM-dd"),
                StatusCode = item.StatusCode,
                SeqNo = item.SeqNo,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            };
        }

        public async Task<bool> CreatePackageAsync(ReservationPackageAdminEditDto dto)
        {
            if (!TryParseDate(dto.EffectiveDate, out var effectiveDate) || !TryParseDate(dto.ExpireDate, out var expireDate))
            {
                return false;
            }

            var now = DateTime.Now;
            _context.ReservationMemberPackages.Add(new ReservationMemberPackage
            {
                MemberId = dto.MemberId,
                ClubId = dto.ClubId,
                PackageName = dto.PackageName.Trim(),
                MembershipName = dto.MembershipName?.Trim(),
                TotalSessions = dto.TotalSessions,
                RemainingSessions = dto.RemainingSessions,
                EffectiveDate = effectiveDate,
                ExpireDate = expireDate,
                StatusCode = dto.StatusCode.Trim(),
                SeqNo = dto.SeqNo,
                CrtTime = now,
                UpdTime = now
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePackageAsync(int id, ReservationPackageAdminEditDto dto)
        {
            var item = await _context.ReservationMemberPackages.FindAsync(id);
            if (item == null || !TryParseDate(dto.EffectiveDate, out var effectiveDate) || !TryParseDate(dto.ExpireDate, out var expireDate))
            {
                return false;
            }

            item.MemberId = dto.MemberId;
            item.ClubId = dto.ClubId;
            item.PackageName = dto.PackageName.Trim();
            item.MembershipName = dto.MembershipName?.Trim();
            item.TotalSessions = dto.TotalSessions;
            item.RemainingSessions = dto.RemainingSessions;
            item.EffectiveDate = effectiveDate;
            item.ExpireDate = expireDate;
            item.StatusCode = dto.StatusCode.Trim();
            item.SeqNo = dto.SeqNo;
            item.UpdTime = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePackageAsync(int id)
        {
            var item = await _context.ReservationMemberPackages.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            var used = await _context.ReservationOrders.AnyAsync(x => x.MemberId == item.MemberId);
            if (used)
            {
                return false;
            }

            _context.ReservationMemberPackages.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ReservationOrderAdminDto>> GetOrderListAsync(int page, int pageSize, ReservationOrderAdminSearchParams? searchParams = null)
        {
            var query = ApplyOrderSearch(_context.ReservationOrders.AsNoTracking(), searchParams);
            var orders = await query
                .OrderByDescending(item => item.ReservationDate)
                .ThenByDescending(item => item.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return await BuildOrderAdminDtosAsync(orders);
        }

        public async Task<int> GetOrderCountAsync(ReservationOrderAdminSearchParams? searchParams = null)
        {
            return await ApplyOrderSearch(_context.ReservationOrders.AsNoTracking(), searchParams).CountAsync();
        }

        public async Task<bool> UpdateOrderStatusAsync(int id, string statusCode)
        {
            var order = await _context.ReservationOrders.FindAsync(id);
            if (order == null)
            {
                return false;
            }

            var normalized = NormalizeStatusCode(statusCode);
            if (normalized != "Upcoming" && normalized != "Completed" && normalized != "Cancelled")
            {
                return false;
            }

            var now = DateTime.Now;
            if (order.StatusCode == normalized)
            {
                return true;
            }

            if (normalized == "Cancelled" && order.StatusCode == "Upcoming")
            {
                if (order.ScheduleSlotId.HasValue)
                {
                    var slot = await _context.ReservationTrainerScheduleSlots.FindAsync(order.ScheduleSlotId.Value);
                    if (slot != null)
                    {
                        slot.IsAvailable = true;
                        slot.UpdTime = now;
                    }
                }

                var package = await _context.ReservationMemberPackages
                    .Where(item => item.MemberId == order.MemberId)
                    .OrderByDescending(item => item.ExpireDate)
                    .FirstOrDefaultAsync();
                if (package != null)
                {
                    package.RemainingSessions += 1;
                    package.UpdTime = now;
                }

                order.CancelTime = now;
            }

            if (normalized == "Completed")
            {
                order.CompleteTime = now;
            }

            order.StatusCode = normalized;
            order.UpdTime = now;
            await _context.SaveChangesAsync();
            return true;
        }

        private IQueryable<ReservationClub> ApplyClubSearch(IQueryable<ReservationClub> query, ReservationClubAdminSearchParams? searchParams)
        {
            if (!string.IsNullOrWhiteSpace(searchParams?.Keyword))
            {
                var keyword = searchParams.Keyword.Trim();
                query = query.Where(item =>
                    item.ClubCode.Contains(keyword) ||
                    item.ClubName.Contains(keyword) ||
                    item.City.Contains(keyword) ||
                    (item.District != null && item.District.Contains(keyword)));
            }

            return query;
        }

        private IQueryable<ReservationTrainerProfile> ApplyTrainerSearch(IQueryable<ReservationTrainerProfile> query, ReservationTrainerAdminSearchParams? searchParams)
        {
            if (searchParams?.ClubId.HasValue == true)
            {
                query = query.Where(item => item.ClubId == searchParams.ClubId.Value);
            }

            if (searchParams?.IsActive.HasValue == true)
            {
                query = query.Where(item => item.IsActive == searchParams.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchParams?.Keyword))
            {
                var keyword = searchParams.Keyword.Trim();
                query = query.Where(item =>
                    item.DisplayName.Contains(keyword) ||
                    item.Title.Contains(keyword) ||
                    (item.TrainingArea != null && item.TrainingArea.Contains(keyword)) ||
                    (item.Highlight != null && item.Highlight.Contains(keyword)));
            }

            return query;
        }

        private IQueryable<ReservationTrainerSessionType> ApplySessionSearch(IQueryable<ReservationTrainerSessionType> query, ReservationSessionAdminSearchParams? searchParams)
        {
            if (searchParams?.TrainerProfileId.HasValue == true)
            {
                query = query.Where(item => item.TrainerProfileId == searchParams.TrainerProfileId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchParams?.Keyword))
            {
                var keyword = searchParams.Keyword.Trim();
                query = query.Where(item => item.SessionCode.Contains(keyword) || item.SessionName.Contains(keyword));
            }

            return query;
        }

        private IQueryable<ReservationTrainerScheduleSlot> ApplyScheduleSearch(IQueryable<ReservationTrainerScheduleSlot> query, ReservationScheduleAdminSearchParams? searchParams)
        {
            if (searchParams?.TrainerProfileId.HasValue == true)
            {
                query = query.Where(item => item.TrainerProfileId == searchParams.TrainerProfileId.Value);
            }

            if (searchParams?.ClubId.HasValue == true)
            {
                query = query.Where(item => item.ClubId == searchParams.ClubId.Value);
            }

            if (searchParams?.IsAvailable.HasValue == true)
            {
                query = query.Where(item => item.IsAvailable == searchParams.IsAvailable.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchParams?.ScheduleDate) && TryParseDate(searchParams.ScheduleDate, out var scheduleDate))
            {
                query = query.Where(item => item.ScheduleDate == scheduleDate);
            }

            return query;
        }

        private IQueryable<ReservationMemberPackage> ApplyPackageSearch(IQueryable<ReservationMemberPackage> query, ReservationPackageAdminSearchParams? searchParams)
        {
            if (searchParams?.MemberId.HasValue == true)
            {
                query = query.Where(item => item.MemberId == searchParams.MemberId.Value);
            }

            if (searchParams?.ClubId.HasValue == true)
            {
                query = query.Where(item => item.ClubId == searchParams.ClubId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchParams?.StatusCode))
            {
                query = query.Where(item => item.StatusCode == searchParams.StatusCode);
            }

            return query;
        }

        private IQueryable<ReservationOrder> ApplyOrderSearch(IQueryable<ReservationOrder> query, ReservationOrderAdminSearchParams? searchParams)
        {
            if (searchParams?.MemberId.HasValue == true)
            {
                query = query.Where(item => item.MemberId == searchParams.MemberId.Value);
            }

            if (searchParams?.TrainerProfileId.HasValue == true)
            {
                query = query.Where(item => item.TrainerProfileId == searchParams.TrainerProfileId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchParams?.StatusCode))
            {
                query = query.Where(item => item.StatusCode == searchParams.StatusCode);
            }

            return query;
        }

        private async Task<List<ReservationTrainerAdminDto>> BuildTrainerAdminDtosAsync(List<ReservationTrainerProfile> profiles)
        {
            if (profiles.Count == 0)
            {
                return new List<ReservationTrainerAdminDto>();
            }

            var userMap = await _context.Users.AsNoTracking()
                .Where(item => profiles.Select(x => x.UserId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);
            var clubMap = await _context.ReservationClubs.AsNoTracking()
                .Where(item => profiles.Select(x => x.ClubId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);
            var tags = await _context.ReservationTrainerTags.AsNoTracking()
                .Where(item => profiles.Select(x => x.Id).Contains(item.TrainerProfileId))
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .ToListAsync();

            var tagLookup = tags.GroupBy(item => item.TrainerProfileId).ToDictionary(group => group.Key, group => group.ToList());

            return profiles.Select(item =>
            {
                tagLookup.TryGetValue(item.Id, out var trainerTags);
                return new ReservationTrainerAdminDto
                {
                    Id = item.Id,
                    UserId = item.UserId,
                    UserName = userMap.TryGetValue(item.UserId, out var user) ? ResolveUserName(user) : string.Empty,
                    DisplayName = item.DisplayName,
                    ClubId = item.ClubId,
                    ClubName = clubMap.TryGetValue(item.ClubId, out var clubName) ? clubName.ClubName : string.Empty,
                    Title = item.Title,
                    Gender = item.Gender,
                    YearsOfExperience = item.YearsOfExperience,
                    Rating = item.Rating,
                    ReviewCount = item.ReviewCount,
                    ServedClients = item.ServedClients,
                    Satisfaction = item.Satisfaction,
                    BasePrice = item.BasePrice,
                    TrainingArea = item.TrainingArea,
                    Highlight = item.Highlight,
                    Introduction = item.Introduction,
                    Story = item.Story,
                    HeroImageUrl = item.HeroImageUrl,
                    HeroTone = item.HeroTone,
                    AccentTone = item.AccentTone,
                    IsRecommended = item.IsRecommended,
                    IsActive = item.IsActive,
                    SeqNo = item.SeqNo,
                    Goals = ExtractTags(trainerTags, "Goal"),
                    Specialties = ExtractTags(trainerTags, "Specialty"),
                    Badges = ExtractTags(trainerTags, "Badge"),
                    Certifications = ExtractTags(trainerTags, "Certification"),
                    CrtTime = item.CrtTime,
                    UpdTime = item.UpdTime
                };
            }).ToList();
        }

        private async Task ReplaceTrainerTagsAsync(int trainerId, ReservationTrainerAdminEditDto dto, DateTime now)
        {
            var existing = await _context.ReservationTrainerTags.Where(item => item.TrainerProfileId == trainerId).ToListAsync();
            _context.ReservationTrainerTags.RemoveRange(existing);

            var created = new List<ReservationTrainerTag>();
            created.AddRange(BuildTagEntities(trainerId, "Goal", dto.Goals, now));
            created.AddRange(BuildTagEntities(trainerId, "Specialty", dto.Specialties, now));
            created.AddRange(BuildTagEntities(trainerId, "Badge", dto.Badges, now));
            created.AddRange(BuildTagEntities(trainerId, "Certification", dto.Certifications, now));

            if (created.Count > 0)
            {
                _context.ReservationTrainerTags.AddRange(created);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<List<ReservationOrderAdminDto>> BuildOrderAdminDtosAsync(List<ReservationOrder> orders)
        {
            if (orders.Count == 0)
            {
                return new List<ReservationOrderAdminDto>();
            }

            var userMap = await _context.Users.AsNoTracking()
                .Where(item => orders.Select(x => x.MemberId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);
            var trainerMap = await _context.ReservationTrainerProfiles.AsNoTracking()
                .Where(item => orders.Select(x => x.TrainerProfileId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);
            var clubMap = await _context.ReservationClubs.AsNoTracking()
                .Where(item => orders.Select(x => x.ClubId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);
            var sessionMap = await _context.ReservationTrainerSessionTypes.AsNoTracking()
                .Where(item => orders.Select(x => x.SessionTypeId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            return orders.Select(item => new ReservationOrderAdminDto
            {
                Id = item.Id,
                ReservationNo = item.ReservationNo,
                MemberId = item.MemberId,
                MemberName = userMap.TryGetValue(item.MemberId, out var user) ? ResolveUserName(user) : string.Empty,
                TrainerProfileId = item.TrainerProfileId,
                TrainerName = trainerMap.TryGetValue(item.TrainerProfileId, out var trainer) ? trainer.DisplayName : string.Empty,
                ClubId = item.ClubId,
                ClubName = clubMap.TryGetValue(item.ClubId, out var club) ? club.ClubName : string.Empty,
                SessionTypeId = item.SessionTypeId,
                SessionName = sessionMap.TryGetValue(item.SessionTypeId, out var session) ? session.SessionName : string.Empty,
                ReservationDate = item.ReservationDate.ToString("yyyy-MM-dd"),
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                PriceAmount = item.PriceAmount,
                StatusCode = item.StatusCode,
                Remark = item.Remark,
                CrtTime = item.CrtTime,
                UpdTime = item.UpdTime
            }).ToList();
        }

        private static List<string> ExtractTags(List<ReservationTrainerTag>? tags, string type)
        {
            if (tags == null)
            {
                return new List<string>();
            }

            return tags.Where(item => item.TagType == type).Select(item => item.TagName).ToList();
        }

        private static List<ReservationTrainerTag> BuildTagEntities(int trainerId, string type, List<string> values, DateTime now)
        {
            return values
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select((item, index) => new ReservationTrainerTag
                {
                    TrainerProfileId = trainerId,
                    TagType = type,
                    TagName = item.Trim(),
                    SeqNo = index + 1,
                    CrtTime = now,
                    UpdTime = now
                })
                .ToList();
        }

        private static bool TryParseDate(string? value, out DateTime date)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static string ResolveUserName(User user)
        {
            return string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        }

        private static string NormalizeStatusCode(string statusCode)
        {
            return (statusCode ?? string.Empty).Trim() switch
            {
                "upcoming" => "Upcoming",
                "completed" => "Completed",
                "cancelled" => "Cancelled",
                _ => statusCode.Trim()
            };
        }
    }
}
