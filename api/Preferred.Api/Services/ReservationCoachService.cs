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
    public class ReservationCoachService : IReservationCoachService
    {
        private readonly ApplicationDbContext _context;

        public ReservationCoachService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReservationCoachDashboardDto?> GetDashboardAsync(int coachUserId)
        {
            var context = await BuildCoachContextAsync(coachUserId);
            if (context == null)
            {
                return null;
            }

            var today = DateTime.Today;
            var reservations = await QueryCoachOrders(context.Value.TrainerProfile.Id).ToListAsync();
            var todayReservations = await BuildCoachReservationDtosAsync(
                reservations.Where(item => item.ReservationDate.Date == today).OrderBy(item => item.StartTime).ToList());

            var boundMemberCount = await _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == context.Value.TrainerProfile.Id)
                .Select(item => item.MemberId)
                .Distinct()
                .CountAsync();

            return new ReservationCoachDashboardDto
            {
                CoachUserId = coachUserId,
                TrainerProfileId = context.Value.TrainerProfile.Id,
                CoachName = ResolveCoachName(context.Value.User, context.Value.TrainerProfile),
                Title = context.Value.TrainerProfile.Title,
                AvatarUrl = context.Value.TrainerProfile.HeroImageUrl ?? context.Value.User.ProfilePictureUrl ?? string.Empty,
                TodayReservationCount = todayReservations.Count,
                UpcomingReservationCount = reservations.Count(item => item.StatusCode == "Upcoming"),
                CompletedReservationCount = reservations.Count(item => item.StatusCode == "Completed"),
                BoundMemberCount = boundMemberCount,
                TodayReservations = todayReservations
            };
        }

        public async Task<List<ReservationCoachMemberDto>> GetMembersAsync(int coachUserId)
        {
            var context = await BuildCoachContextAsync(coachUserId);
            if (context == null)
            {
                return new List<ReservationCoachMemberDto>();
            }

            var memberIds = await _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == context.Value.TrainerProfile.Id)
                .Select(item => item.MemberId)
                .Distinct()
                .ToListAsync();

            if (memberIds.Count == 0)
            {
                return new List<ReservationCoachMemberDto>();
            }

            var members = await _context.Users
                .AsNoTracking()
                .Where(item => memberIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var packages = await _context.ReservationMemberPackages
                .AsNoTracking()
                .Where(item => memberIds.Contains(item.MemberId))
                .GroupBy(item => item.MemberId)
                .Select(group => group.OrderByDescending(item => item.ExpireDate).First())
                .ToListAsync();

            var latestOrders = await _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == context.Value.TrainerProfile.Id && memberIds.Contains(item.MemberId))
                .GroupBy(item => item.MemberId)
                .Select(group => group.OrderByDescending(item => item.ReservationDate).ThenByDescending(item => item.StartTime).First())
                .ToListAsync();

            var packageMap = packages.ToDictionary(item => item.MemberId);
            var orderMap = latestOrders.ToDictionary(item => item.MemberId);

            return memberIds
                .Where(members.ContainsKey)
                .Select(memberId =>
                {
                    var member = members[memberId];
                    packageMap.TryGetValue(memberId, out var package);
                    orderMap.TryGetValue(memberId, out var latestOrder);

                    return new ReservationCoachMemberDto
                    {
                        MemberId = memberId,
                        MemberName = ResolveMemberName(member),
                        PhoneNumber = member.PhoneNumber ?? string.Empty,
                        AvatarUrl = member.ProfilePictureUrl ?? string.Empty,
                        MembershipName = package?.MembershipName ?? package?.PackageName ?? "普通会员",
                        RemainingSessions = package?.RemainingSessions ?? 0,
                        LatestReservation = latestOrder == null
                            ? string.Empty
                            : $"{latestOrder.ReservationDate:yyyy-MM-dd} {latestOrder.StartTime}"
                    };
                })
                .OrderBy(item => item.MemberName)
                .ToList();
        }

        public async Task<ReservationCoachScheduleDto?> GetScheduleAsync(int coachUserId, string? scheduleDate = null)
        {
            var context = await BuildCoachContextAsync(coachUserId);
            if (context == null)
            {
                return null;
            }

            var targetDate = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(scheduleDate) &&
                DateTime.TryParseExact(scheduleDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                targetDate = parsedDate.Date;
            }

            var slots = await _context.ReservationTrainerScheduleSlots
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == context.Value.TrainerProfile.Id && item.ScheduleDate == targetDate)
                .OrderBy(item => item.StartTime)
                .ToListAsync();

            var orders = await _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == context.Value.TrainerProfile.Id && item.ReservationDate == targetDate)
                .ToListAsync();

            var memberIds = orders.Select(item => item.MemberId).Distinct().ToList();
            var sessionIds = orders.Select(item => item.SessionTypeId).Distinct().ToList();
            var memberMap = await _context.Users.AsNoTracking().Where(item => memberIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
            var sessionMap = await _context.ReservationTrainerSessionTypes.AsNoTracking().Where(item => sessionIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);

            var orderLookup = orders.ToDictionary(item => $"{item.StartTime}-{item.EndTime}", item => item);

            return new ReservationCoachScheduleDto
            {
                ScheduleDate = targetDate.ToString("yyyy-MM-dd"),
                Items = slots.Select(slot =>
                {
                    orderLookup.TryGetValue($"{slot.StartTime}-{slot.EndTime}", out var order);
                    return new ReservationCoachScheduleItemDto
                    {
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        IsReserved = order != null && order.StatusCode != "Cancelled",
                        MemberName = order != null && memberMap.ContainsKey(order.MemberId) ? ResolveMemberName(memberMap[order.MemberId]) : string.Empty,
                        SessionName = order != null && sessionMap.ContainsKey(order.SessionTypeId) ? sessionMap[order.SessionTypeId].SessionName : string.Empty
                    };
                }).ToList()
            };
        }

        public async Task<List<ReservationCoachReservationDto>> GetReservationsAsync(int coachUserId, string? statusCode = null)
        {
            var context = await BuildCoachContextAsync(coachUserId);
            if (context == null)
            {
                return new List<ReservationCoachReservationDto>();
            }

            var query = QueryCoachOrders(context.Value.TrainerProfile.Id);
            if (!string.IsNullOrWhiteSpace(statusCode))
            {
                query = query.Where(item => item.StatusCode == NormalizeStatusCode(statusCode));
            }

            return await BuildCoachReservationDtosAsync(await query.ToListAsync());
        }

        public async Task<ReservationCoachProfileDto?> GetProfileAsync(int coachUserId)
        {
            var context = await BuildCoachContextAsync(coachUserId);
            if (context == null)
            {
                return null;
            }

            var club = await _context.ReservationClubs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == context.Value.TrainerProfile.ClubId);

            return new ReservationCoachProfileDto
            {
                CoachUserId = coachUserId,
                TrainerProfileId = context.Value.TrainerProfile.Id,
                CoachName = ResolveCoachName(context.Value.User, context.Value.TrainerProfile),
                UserName = context.Value.User.UserName,
                Email = context.Value.User.Email,
                PhoneNumber = context.Value.User.PhoneNumber ?? string.Empty,
                AvatarUrl = context.Value.TrainerProfile.HeroImageUrl ?? context.Value.User.ProfilePictureUrl ?? string.Empty,
                ClubName = club?.ClubName ?? string.Empty,
                Title = context.Value.TrainerProfile.Title,
                Highlight = context.Value.TrainerProfile.Highlight ?? string.Empty
            };
        }

        private IQueryable<ReservationOrder> QueryCoachOrders(int trainerProfileId)
        {
            return _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfileId)
                .OrderBy(item => item.StatusCode == "Upcoming" ? 0 : item.StatusCode == "Completed" ? 1 : 2)
                .ThenBy(item => item.ReservationDate)
                .ThenBy(item => item.StartTime);
        }

        private async Task<List<ReservationCoachReservationDto>> BuildCoachReservationDtosAsync(List<ReservationOrder> orders)
        {
            if (orders.Count == 0)
            {
                return new List<ReservationCoachReservationDto>();
            }

            var memberIds = orders.Select(item => item.MemberId).Distinct().ToList();
            var clubIds = orders.Select(item => item.ClubId).Distinct().ToList();
            var sessionIds = orders.Select(item => item.SessionTypeId).Distinct().ToList();

            var members = await _context.Users.AsNoTracking().Where(item => memberIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
            var clubs = await _context.ReservationClubs.AsNoTracking().Where(item => clubIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
            var sessions = await _context.ReservationTrainerSessionTypes.AsNoTracking().Where(item => sessionIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);

            return orders.Select(item =>
            {
                members.TryGetValue(item.MemberId, out var member);
                clubs.TryGetValue(item.ClubId, out var club);
                sessions.TryGetValue(item.SessionTypeId, out var session);

                return new ReservationCoachReservationDto
                {
                    Id = item.Id,
                    ReservationNo = item.ReservationNo,
                    MemberId = item.MemberId,
                    MemberName = ResolveMemberName(member),
                    MemberAvatarUrl = member?.ProfilePictureUrl ?? string.Empty,
                    SessionName = session?.SessionName ?? string.Empty,
                    ReservationDate = item.ReservationDate.ToString("yyyy-MM-dd"),
                    TimeRange = $"{item.StartTime} - {item.EndTime}",
                    StatusCode = item.StatusCode,
                    StatusLabel = BuildCoachStatusLabel(item.StatusCode),
                    ClubName = club?.ClubName ?? string.Empty,
                    Remark = item.Remark ?? string.Empty
                };
            }).ToList();
        }

        private async Task<(User User, ReservationTrainerProfile TrainerProfile)?> BuildCoachContextAsync(int coachUserId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == coachUserId);
            var trainerProfile = await _context.ReservationTrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == coachUserId && item.IsActive);
            if (user == null || trainerProfile == null)
            {
                return null;
            }

            return (user, trainerProfile);
        }

        private static string ResolveMemberName(User? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        }

        private static string ResolveCoachName(User user, ReservationTrainerProfile trainerProfile)
        {
            if (!string.IsNullOrWhiteSpace(trainerProfile.DisplayName))
            {
                return trainerProfile.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                return user.FullName;
            }

            return user.UserName;
        }

        private static string NormalizeStatusCode(string statusCode)
        {
            return (statusCode ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "completed" => "Completed",
                "cancelled" => "Cancelled",
                _ => "Upcoming"
            };
        }

        private static string BuildCoachStatusLabel(string statusCode)
        {
            return statusCode switch
            {
                "Completed" => "已完成",
                "Cancelled" => "已取消",
                _ => "待上课"
            };
        }
    }
}
