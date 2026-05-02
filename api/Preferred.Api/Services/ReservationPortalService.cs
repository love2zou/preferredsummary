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
    public class ReservationPortalService : IReservationPortalService
    {
        private readonly ApplicationDbContext _context;

        public ReservationPortalService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReservationFlowResponseDto?> GetMemberFlowAsync(int memberId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == memberId);
            if (user == null)
            {
                return null;
            }

            var upcomingOrder = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.MemberId == memberId && (item.StatusCode == "Upcoming" || item.StatusCode == "Pending"))
                .OrderBy(item => item.ReservationDate)
                .ThenBy(item => item.StartTime)
                .FirstOrDefaultAsync();

            var completedOrder = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.MemberId == memberId && item.StatusCode == "Completed")
                .OrderByDescending(item => item.ReservationDate)
                .ThenByDescending(item => item.StartTime)
                .FirstOrDefaultAsync();

            var checkIn = await _context.ReservationCheckInRecords.AsNoTracking()
                .Where(item => item.MemberId == memberId)
                .OrderByDescending(item => item.CheckInTime)
                .FirstOrDefaultAsync();

            var coachUserId = 0;
            if (upcomingOrder != null)
            {
                coachUserId = await _context.ReservationTrainerProfiles.AsNoTracking()
                    .Where(item => item.Id == upcomingOrder.TrainerProfileId)
                    .Select(item => item.UserId)
                    .FirstOrDefaultAsync();
            }

            var messages = coachUserId <= 0
                ? new List<ReservationConversationMessage>()
                : await _context.ReservationConversationMessages.AsNoTracking()
                    .Where(item => item.MemberId == memberId && item.CoachUserId == coachUserId)
                    .OrderBy(item => item.SentTime)
                    .Take(12)
                    .ToListAsync();

            return new ReservationFlowResponseDto
            {
                UpcomingReservation = await BuildOrderItemAsync(upcomingOrder),
                CompletedReservation = await BuildOrderItemAsync(completedOrder),
                CheckIn = checkIn == null
                    ? null
                    : new ReservationCheckInDto
                    {
                        ReservationOrderId = checkIn.ReservationOrderId,
                        CoachName = await ResolveCoachNameByUserIdAsync(checkIn.CoachUserId),
                        CheckInMethod = checkIn.CheckInMethod,
                        CheckInTime = checkIn.CheckInTime.ToString("yyyy-MM-dd HH:mm"),
                        ClubName = checkIn.ClubName,
                        AreaName = checkIn.AreaName
                    },
                Messages = messages.Select(item => new ReservationConversationMessageDto
                {
                    SenderRole = item.SenderRole,
                    SenderName = item.SenderName,
                    Content = item.Content,
                    SentTime = item.SentTime.ToString("HH:mm")
                }).ToList(),
                CancelReasons = new List<string> { "时间冲突", "身体不适", "临时有事", "更换教练", "其他原因" },
                ReviewMetrics = new List<ReservationReviewMetricDto>
                {
                    new ReservationReviewMetricDto { Label = "教练专业度", Score = 5, Description = "非常满意" },
                    new ReservationReviewMetricDto { Label = "服务态度", Score = 5, Description = "非常满意" },
                    new ReservationReviewMetricDto { Label = "训练效果", Score = 4, Description = "满意" },
                    new ReservationReviewMetricDto { Label = "课程强度", Score = 4, Description = "满意" }
                },
                ReviewTags = new List<string> { "专业", "耐心", "动作讲解清晰", "效果好", "氛围轻松", "强度合适" }
            };
        }

        public async Task<ReservationCommerceResponseDto> GetCommerceAsync(int memberId)
        {
            var packages = await _context.ReservationCoursePackages.AsNoTracking()
                .Where(item => item.StatusCode == "Active")
                .OrderByDescending(item => item.IsRecommended)
                .ThenBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .ToListAsync();

            var orders = await _context.ReservationCourseOrders.AsNoTracking()
                .Where(item => item.MemberId == memberId)
                .OrderByDescending(item => item.OrderTime)
                .ThenByDescending(item => item.Id)
                .ToListAsync();

            var coupons = await (from memberCoupon in _context.ReservationMemberCoupons.AsNoTracking()
                                 join coupon in _context.ReservationCoupons.AsNoTracking() on memberCoupon.CouponId equals coupon.Id
                                 where memberCoupon.MemberId == memberId
                                 orderby memberCoupon.StatusCode, coupon.EndDate
                                 select new ReservationMemberCouponDto
                                 {
                                     CouponId = coupon.Id,
                                     Title = coupon.Title,
                                     CouponType = coupon.CouponType,
                                     DiscountValue = coupon.DiscountValue,
                                     MinAmount = coupon.MinAmount,
                                     RuleText = coupon.RuleText,
                                     StartDate = coupon.StartDate.ToString("yyyy-MM-dd"),
                                     EndDate = coupon.EndDate.ToString("yyyy-MM-dd"),
                                     StatusCode = memberCoupon.StatusCode
                                 }).ToListAsync();

            var packageMap = packages.ToDictionary(item => item.Id);

            return new ReservationCommerceResponseDto
            {
                Packages = packages.Select(MapCoursePackage).ToList(),
                FeaturedPackage = packages.Where(item => item.IsRecommended).Select(MapCoursePackage).FirstOrDefault()
                    ?? packages.Select(MapCoursePackage).FirstOrDefault(),
                Orders = orders.Select(item =>
                {
                    packageMap.TryGetValue(item.CoursePackageId, out var package);
                    return new ReservationCourseOrderDto
                    {
                        Id = item.Id,
                        OrderNo = item.OrderNo,
                        PackageName = package?.PackageName ?? string.Empty,
                        CoverImageUrl = package?.CoverImageUrl ?? string.Empty,
                        OriginAmount = item.OriginAmount,
                        DiscountAmount = item.DiscountAmount,
                        PointDiscountAmount = item.PointDiscountAmount,
                        PayAmount = item.PayAmount,
                        PaymentMethod = item.PaymentMethod,
                        StatusCode = item.StatusCode,
                        OrderTime = item.OrderTime.ToString("yyyy-MM-dd HH:mm")
                    };
                }).ToList(),
                Coupons = coupons
            };
        }

        public async Task<ReservationMemberCenterResponseDto?> GetMemberCenterAsync(int memberId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == memberId);
            if (user == null)
            {
                return null;
            }

            var activePackage = await _context.ReservationMemberPackages.AsNoTracking()
                .Where(item => item.MemberId == memberId)
                .OrderByDescending(item => item.ExpireDate)
                .FirstOrDefaultAsync();

            var clubName = activePackage == null
                ? string.Empty
                : await _context.ReservationClubs.AsNoTracking().Where(item => item.Id == activePackage.ClubId).Select(item => item.ClubName).FirstOrDefaultAsync() ?? string.Empty;

            var bodyMetrics = await _context.ReservationBodyMetrics.AsNoTracking()
                .Where(item => item.MemberId == memberId)
                .OrderByDescending(item => item.MeasureTime)
                .ThenByDescending(item => item.Id)
                .ToListAsync();

            var trainingRecords = await _context.ReservationTrainingRecords.AsNoTracking()
                .Where(item => item.MemberId == memberId)
                .OrderByDescending(item => item.RecordTime)
                .ThenByDescending(item => item.Id)
                .ToListAsync();

            var coachUserIds = trainingRecords.Select(item => item.CoachUserId).Distinct().ToList();
            var coachUsers = await _context.Users.AsNoTracking()
                .Where(item => coachUserIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var trainingPlan = await _context.ReservationTrainingPlans.AsNoTracking()
                .Where(item => item.MemberId == memberId && item.StatusCode == "Active")
                .OrderByDescending(item => item.UpdTime)
                .ThenByDescending(item => item.Id)
                .FirstOrDefaultAsync();

            var planItems = trainingPlan == null
                ? new List<ReservationTrainingPlanItem>()
                : await _context.ReservationTrainingPlanItems.AsNoTracking()
                    .Where(item => item.PlanId == trainingPlan.Id)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Id)
                    .ToListAsync();

            var notifications = await _context.Notifications.AsNoTracking()
                .Where(item => item.Receiver == user.UserName)
                .OrderByDescending(item => item.SendTime)
                .Take(8)
                .ToListAsync();

            var latestMetric = bodyMetrics.FirstOrDefault();

            return new ReservationMemberCenterResponseDto
            {
                Profile = new ReservationMemberProfileDto
                {
                    MemberId = memberId,
                    Name = ResolveUserDisplayName(user),
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    AvatarUrl = user.ProfilePictureUrl ?? string.Empty,
                    Gender = "男",
                    Age = 28,
                    HeightCm = 175,
                    WeightKg = latestMetric?.WeightKg ?? 72.5m,
                    Birthday = "1996-06-18",
                    City = clubName.Contains("深圳") ? "深圳市" : "上海市",
                    MembershipName = activePackage?.MembershipName ?? "普通会员",
                    HealthRemark = "无慢性疾病，左膝轻微旧伤，运动前请做好热身。",
                    PrimaryGoal = "减脂塑形",
                    SecondaryGoals = new List<string> { "康复训练", "提升体能" }
                },
                BodyMetrics = bodyMetrics.Select(item => new ReservationBodyMetricDto
                {
                    MeasureTime = item.MeasureTime.ToString("yyyy-MM-dd HH:mm"),
                    WeightKg = item.WeightKg,
                    BodyFatRate = item.BodyFatRate,
                    Bmi = item.Bmi,
                    MuscleKg = item.MuscleKg
                }).ToList(),
                TrainingRecords = trainingRecords.Select(item => new ReservationTrainingRecordDto
                {
                    RecordTime = item.RecordTime.ToString("yyyy-MM-dd HH:mm"),
                    Title = item.Title,
                    CoachName = coachUsers.TryGetValue(item.CoachUserId, out var coachUser) ? ResolveUserDisplayName(coachUser) : string.Empty,
                    DurationMinutes = item.DurationMinutes,
                    Calories = item.Calories,
                    LocationName = item.LocationName,
                    StatusCode = item.StatusCode,
                    Summary = item.Summary
                }).ToList(),
                TrainingPlan = trainingPlan == null
                    ? null
                    : new ReservationTrainingPlanDto
                    {
                        PlanName = trainingPlan.PlanName,
                        Goal = trainingPlan.Goal,
                        StartDate = trainingPlan.StartDate.ToString("yyyy-MM-dd"),
                        EndDate = trainingPlan.EndDate.ToString("yyyy-MM-dd"),
                        ProgressText = trainingPlan.ProgressText,
                        Items = planItems.Select(item => new ReservationTrainingPlanItemDto
                        {
                            DayLabel = item.DayLabel,
                            Title = item.Title,
                            DurationMinutes = item.DurationMinutes,
                            CaloriesTarget = item.CaloriesTarget,
                            IsCompleted = item.IsCompleted
                        }).ToList()
                    },
                Notifications = notifications.Any()
                    ? notifications.Select(item => new ReservationMemberNotificationDto
                    {
                        Title = item.Name,
                        Content = item.Content,
                        NotifyType = item.NotifyType,
                        SendTime = item.SendTime.ToString("yyyy-MM-dd HH:mm")
                    }).ToList()
                    : new List<ReservationMemberNotificationDto>
                    {
                        new ReservationMemberNotificationDto { Title = "课程预约成功", Content = "您已成功预约下周一 10:00 的减脂塑形私教课。", NotifyType = "Reservation", SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm") },
                        new ReservationMemberNotificationDto { Title = "上课提醒", Content = "预约课程将在 1 小时后开始，请提前到店热身。", NotifyType = "Reminder", SendTime = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm") }
                    }
            };
        }

        public async Task<ReservationCoachWorkbenchResponseDto?> GetCoachWorkbenchAsync(int coachUserId)
        {
            var coachUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == coachUserId);
            var trainerProfile = await _context.ReservationTrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == coachUserId && item.IsActive);
            if (coachUser == null || trainerProfile == null)
            {
                return null;
            }

            var dashboard = await BuildCoachDashboardAsync(coachUserId, trainerProfile, coachUser);
            var schedule = await BuildCoachScheduleAsync(trainerProfile.Id, DateTime.Today);

            var pendingOrders = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfile.Id && item.StatusCode == "Pending")
                .OrderBy(item => item.ReservationDate)
                .ThenBy(item => item.StartTime)
                .ToListAsync();

            var members = await BuildCoachMembersAsync(coachUserId, trainerProfile.Id);
            var records = await BuildCoachRecordsAsync(coachUserId);
            var plan = await BuildCoachPlanAsync(coachUserId);
            var messages = await _context.ReservationConversationMessages.AsNoTracking()
                .Where(item => item.CoachUserId == coachUserId)
                .OrderByDescending(item => item.SentTime)
                .Take(12)
                .ToListAsync();

            return new ReservationCoachWorkbenchResponseDto
            {
                Dashboard = dashboard,
                Schedule = schedule,
                PendingAudits = await BuildCoachReservationDtosAsync(pendingOrders),
                Members = members,
                Records = records,
                TrainingPlan = plan,
                Messages = messages
                    .OrderBy(item => item.SentTime)
                    .Select(item => new ReservationConversationMessageDto
                    {
                        SenderRole = item.SenderRole,
                        SenderName = item.SenderName,
                        Content = item.Content,
                        SentTime = item.SentTime.ToString("HH:mm")
                    }).ToList()
            };
        }

        private async Task<ReservationCoachDashboardDto> BuildCoachDashboardAsync(int coachUserId, ReservationTrainerProfile trainerProfile, User coachUser)
        {
            var orders = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfile.Id)
                .ToListAsync();

            var todayOrders = orders
                .Where(item => item.ReservationDate.Date == DateTime.Today)
                .OrderBy(item => item.StartTime)
                .ToList();

            var boundMemberCount = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfile.Id)
                .Select(item => item.MemberId)
                .Distinct()
                .CountAsync();

            return new ReservationCoachDashboardDto
            {
                CoachUserId = coachUserId,
                TrainerProfileId = trainerProfile.Id,
                CoachName = ResolveUserDisplayName(coachUser),
                Title = trainerProfile.Title,
                AvatarUrl = trainerProfile.HeroImageUrl ?? coachUser.ProfilePictureUrl ?? string.Empty,
                TodayReservationCount = todayOrders.Count,
                UpcomingReservationCount = orders.Count(item => item.StatusCode == "Upcoming"),
                CompletedReservationCount = orders.Count(item => item.StatusCode == "Completed"),
                BoundMemberCount = boundMemberCount,
                TodayReservations = await BuildCoachReservationDtosAsync(todayOrders)
            };
        }

        private async Task<ReservationCoachScheduleDto> BuildCoachScheduleAsync(int trainerProfileId, DateTime targetDate)
        {
            var slots = await _context.ReservationTrainerScheduleSlots.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfileId && item.ScheduleDate == targetDate.Date)
                .OrderBy(item => item.StartTime)
                .ToListAsync();

            var orders = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfileId && item.ReservationDate == targetDate.Date && item.StatusCode != "Cancelled")
                .ToListAsync();

            var memberIds = orders.Select(item => item.MemberId).Distinct().ToList();
            var sessionIds = orders.Select(item => item.SessionTypeId).Distinct().ToList();
            var users = await _context.Users.AsNoTracking().Where(item => memberIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
            var sessions = await _context.ReservationTrainerSessionTypes.AsNoTracking().Where(item => sessionIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);

            return new ReservationCoachScheduleDto
            {
                ScheduleDate = targetDate.ToString("yyyy-MM-dd"),
                Items = slots.Select(slot =>
                {
                    var order = orders.FirstOrDefault(item => item.StartTime == slot.StartTime && item.EndTime == slot.EndTime);
                    return new ReservationCoachScheduleItemDto
                    {
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        IsReserved = order != null,
                        MemberName = order != null && users.ContainsKey(order.MemberId) ? ResolveUserDisplayName(users[order.MemberId]) : string.Empty,
                        SessionName = order != null && sessions.ContainsKey(order.SessionTypeId) ? sessions[order.SessionTypeId].SessionName : string.Empty
                    };
                }).ToList()
            };
        }

        private async Task<List<ReservationCoachMemberDto>> BuildCoachMembersAsync(int coachUserId, int trainerProfileId)
        {
            var memberIds = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfileId)
                .Select(item => item.MemberId)
                .Distinct()
                .ToListAsync();

            if (memberIds.Count == 0)
            {
                return new List<ReservationCoachMemberDto>();
            }

            var users = await _context.Users.AsNoTracking().Where(item => memberIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
            var packages = await _context.ReservationMemberPackages.AsNoTracking()
                .Where(item => memberIds.Contains(item.MemberId))
                .GroupBy(item => item.MemberId)
                .Select(group => group.OrderByDescending(item => item.ExpireDate).First())
                .ToListAsync();

            var latestOrders = await _context.ReservationOrders.AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerProfileId && memberIds.Contains(item.MemberId))
                .GroupBy(item => item.MemberId)
                .Select(group => group.OrderByDescending(item => item.ReservationDate).ThenByDescending(item => item.StartTime).First())
                .ToListAsync();

            var packageMap = packages.ToDictionary(item => item.MemberId);
            var orderMap = latestOrders.ToDictionary(item => item.MemberId);

            return memberIds
                .Where(users.ContainsKey)
                .Select(memberId =>
                {
                    packageMap.TryGetValue(memberId, out var package);
                    orderMap.TryGetValue(memberId, out var order);
                    return new ReservationCoachMemberDto
                    {
                        MemberId = memberId,
                        MemberName = ResolveUserDisplayName(users[memberId]),
                        PhoneNumber = users[memberId].PhoneNumber ?? string.Empty,
                        AvatarUrl = users[memberId].ProfilePictureUrl ?? string.Empty,
                        MembershipName = package?.MembershipName ?? package?.PackageName ?? "普通会员",
                        RemainingSessions = package?.RemainingSessions ?? 0,
                        LatestReservation = order == null ? string.Empty : $"{order.ReservationDate:yyyy-MM-dd} {order.StartTime}"
                    };
                }).ToList();
        }

        private async Task<List<ReservationTrainingRecordDto>> BuildCoachRecordsAsync(int coachUserId)
        {
            var records = await _context.ReservationTrainingRecords.AsNoTracking()
                .Where(item => item.CoachUserId == coachUserId)
                .OrderByDescending(item => item.RecordTime)
                .Take(12)
                .ToListAsync();

            var memberIds = records.Select(item => item.MemberId).Distinct().ToList();
            var users = await _context.Users.AsNoTracking().Where(item => memberIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);

            return records.Select(item => new ReservationTrainingRecordDto
            {
                RecordTime = item.RecordTime.ToString("yyyy-MM-dd HH:mm"),
                Title = $"{(users.ContainsKey(item.MemberId) ? ResolveUserDisplayName(users[item.MemberId]) : "学员")} · {item.Title}",
                CoachName = string.Empty,
                DurationMinutes = item.DurationMinutes,
                Calories = item.Calories,
                LocationName = item.LocationName,
                StatusCode = item.StatusCode,
                Summary = item.Summary
            }).ToList();
        }

        private async Task<ReservationTrainingPlanDto?> BuildCoachPlanAsync(int coachUserId)
        {
            var plan = await _context.ReservationTrainingPlans.AsNoTracking()
                .Where(item => item.CoachUserId == coachUserId && item.StatusCode == "Active")
                .OrderByDescending(item => item.UpdTime)
                .FirstOrDefaultAsync();

            if (plan == null)
            {
                return null;
            }

            var items = await _context.ReservationTrainingPlanItems.AsNoTracking()
                .Where(item => item.PlanId == plan.Id)
                .OrderBy(item => item.SortOrder)
                .ToListAsync();

            return new ReservationTrainingPlanDto
            {
                PlanName = plan.PlanName,
                Goal = plan.Goal,
                StartDate = plan.StartDate.ToString("yyyy-MM-dd"),
                EndDate = plan.EndDate.ToString("yyyy-MM-dd"),
                ProgressText = plan.ProgressText,
                Items = items.Select(item => new ReservationTrainingPlanItemDto
                {
                    DayLabel = item.DayLabel,
                    Title = item.Title,
                    DurationMinutes = item.DurationMinutes,
                    CaloriesTarget = item.CaloriesTarget,
                    IsCompleted = item.IsCompleted
                }).ToList()
            };
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
                    MemberName = ResolveUserDisplayName(member),
                    MemberAvatarUrl = member?.ProfilePictureUrl ?? string.Empty,
                    SessionName = session?.SessionName ?? string.Empty,
                    ReservationDate = item.ReservationDate.ToString("yyyy-MM-dd"),
                    TimeRange = $"{item.StartTime} - {item.EndTime}",
                    StatusCode = item.StatusCode,
                    StatusLabel = item.StatusCode,
                    ClubName = club?.ClubName ?? string.Empty,
                    Remark = item.Remark ?? string.Empty
                };
            }).ToList();
        }

        private async Task<ReservationOrderItemDto?> BuildOrderItemAsync(ReservationOrder? order)
        {
            if (order == null)
            {
                return null;
            }

            var trainerProfile = await _context.ReservationTrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == order.TrainerProfileId);
            var trainerUser = trainerProfile == null
                ? null
                : await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == trainerProfile.UserId);
            var club = await _context.ReservationClubs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == order.ClubId);
            var sessionType = await _context.ReservationTrainerSessionTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == order.SessionTypeId);

            return new ReservationOrderItemDto
            {
                Id = order.Id,
                ReservationNo = order.ReservationNo,
                TrainerId = order.TrainerProfileId,
                TrainerName = trainerProfile != null ? (string.IsNullOrWhiteSpace(trainerProfile.DisplayName) ? ResolveUserDisplayName(trainerUser) : trainerProfile.DisplayName) : string.Empty,
                TrainerPhotoUrl = trainerProfile?.HeroImageUrl ?? trainerUser?.ProfilePictureUrl ?? string.Empty,
                SessionTypeId = order.SessionTypeId,
                SessionLabel = sessionType?.SessionName ?? string.Empty,
                DateLabel = BuildDateLabel(order.ReservationDate),
                CalendarDate = order.ReservationDate.ToString("yyyy-MM-dd"),
                TimeRange = $"{order.StartTime} - {order.EndTime}",
                Club = club?.ClubName ?? string.Empty,
                Area = trainerProfile?.TrainingArea ?? string.Empty,
                Status = order.StatusCode == "Completed" ? "completed" : order.StatusCode == "Cancelled" ? "cancelled" : "upcoming",
                Tag = order.StatusCode,
                Note = order.Remark ?? string.Empty,
                Price = order.PriceAmount
            };
        }

        private ReservationCoursePackageDto MapCoursePackage(ReservationCoursePackage item)
        {
            return new ReservationCoursePackageDto
            {
                Id = item.Id,
                PackageCode = item.PackageCode,
                PackageName = item.PackageName,
                CategoryName = item.CategoryName,
                Summary = item.Summary,
                CoverImageUrl = item.CoverImageUrl,
                OriginalPrice = item.OriginalPrice,
                SalePrice = item.SalePrice,
                TotalSessions = item.TotalSessions,
                ValidDays = item.ValidDays,
                CoachLevel = item.CoachLevel,
                ClubScope = item.ClubScope,
                IsRecommended = item.IsRecommended,
                StatusCode = item.StatusCode
            };
        }

        private async Task<string> ResolveCoachNameByUserIdAsync(int coachUserId)
        {
            var profile = await _context.ReservationTrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == coachUserId && item.IsActive);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                return profile.DisplayName;
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == coachUserId);
            return ResolveUserDisplayName(user);
        }

        private static string ResolveUserDisplayName(User? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        }

        private static string BuildDateLabel(DateTime date)
        {
            var today = DateTime.Today;
            if (date.Date == today)
            {
                return "今天";
            }

            if (date.Date == today.AddDays(1))
            {
                return "明天";
            }

            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                DayOfWeek.Sunday => "周日",
                _ => date.ToString("MM-dd", CultureInfo.InvariantCulture)
            };
        }
    }
}
