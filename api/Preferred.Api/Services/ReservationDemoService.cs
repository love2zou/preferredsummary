using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public class ReservationDemoService : IReservationDemoService
    {
        private readonly ApplicationDbContext _context;

        public ReservationDemoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReservationDemoSeedResultDto> SeedDemoDataAsync()
        {
            var now = DateTime.Now;
            const string password = "Test123456";

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var clubA = await EnsureClubAsync("SH-PD-001", "世纪大道店", "上海", "浦东新区", "上海市浦东新区世纪大道100号", "06:30-22:30", 10, now);
            var clubB = await EnsureClubAsync("SH-XH-001", "徐家汇店", "上海", "徐汇区", "上海市徐汇区虹桥路88号", "07:00-22:00", 20, now);

            var memberA = await EnsureUserAsync("app_member_01", "张小力", "member01@example.com", "13810001001", password, "Member", "reservation-app", "https://images.unsplash.com/photo-1566753323558-f4e0952af115?auto=format&fit=crop&w=320&q=80", now);
            var memberB = await EnsureUserAsync("app_member_02", "林悦", "member02@example.com", "13810001002", password, "Member", "reservation-app", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=320&q=80", now);
            var coachA = await EnsureUserAsync("app_coach_01", "张扬", "coach01@example.com", "13810002001", password, "Coach", "reservation-app", "https://images.unsplash.com/photo-1567013127542-490d757e51fc?auto=format&fit=crop&w=640&q=80", now);
            var coachB = await EnsureUserAsync("app_coach_02", "李想", "coach02@example.com", "13810002002", password, "Coach", "reservation-app", "https://images.unsplash.com/photo-1548690312-e3b507d8c110?auto=format&fit=crop&w=640&q=80", now);
            await _context.SaveChangesAsync();

            var trainerA = await EnsureTrainerProfileAsync(coachA.Id, clubA.Id, "张扬", "高级私教", "男", 6, 4.9m, 568, 1023, 98, 280m, "私教区A", "减脂塑形", "擅长减脂塑形、增肌强化与体态调整，课程节奏稳定，适合长期训练习惯养成。", "更强调动作基础和训练连贯性，适合希望稳步变化的会员。", coachA.ProfilePictureUrl ?? string.Empty, "#dff4e6", "#0f8a43", true, 10, now);
            var trainerB = await EnsureTrainerProfileAsync(coachB.Id, clubA.Id, "李想", "高级私教", "女", 5, 4.9m, 432, 852, 96, 260m, "私教区B", "体能提升", "擅长女性力量训练与功能性提升，强调动作控制和训练效率。", "课程节奏清晰利落，适合希望提升力量和训练效率的会员。", coachB.ProfilePictureUrl ?? string.Empty, "#ffe6d8", "#ff7a21", true, 20, now);

            await EnsureTrainerTagsAsync(trainerA.Id, new[]
            {
                ("Goal", "减脂"),
                ("Goal", "增肌"),
                ("Specialty", "减脂"),
                ("Specialty", "增肌"),
                ("Specialty", "体态矫正"),
                ("Badge", "高投入人气教练"),
                ("Badge", "认证教练"),
                ("Certification", "ACE"),
                ("Certification", "NASM"),
                ("Certification", "CPR")
            }, now);

            await EnsureTrainerTagsAsync(trainerB.Id, new[]
            {
                ("Goal", "增肌"),
                ("Goal", "体能提升"),
                ("Specialty", "增肌"),
                ("Specialty", "体能提升"),
                ("Badge", "高投入人气教练"),
                ("Badge", "认证教练"),
                ("Certification", "ACE"),
                ("Certification", "TRX"),
                ("Certification", "CPR")
            }, now);

            var trainerABodyShape = await EnsureSessionTypeAsync(trainerA.Id, "body-shape", "体态课", "体态改善与核心稳定", 60, 280m, 10, now);
            var trainerARegular = await EnsureSessionTypeAsync(trainerA.Id, "regular", "常规私教课", "综合训练计划安排", 60, 280m, 20, now);
            var trainerAAssessment = await EnsureSessionTypeAsync(trainerA.Id, "assessment", "体测评估", "训练前数据评估", 60, 180m, 30, now);
            var trainerBStrength = await EnsureSessionTypeAsync(trainerB.Id, "strength", "女性力量课", "力量提升与稳定控制", 60, 260m, 10, now);
            var trainerBRegular = await EnsureSessionTypeAsync(trainerB.Id, "regular", "常规私教课", "综合训练计划安排", 60, 260m, 20, now);
            var trainerBAssessment = await EnsureSessionTypeAsync(trainerB.Id, "assessment", "功能评估", "动作基础评估反馈", 60, 180m, 30, now);

            await EnsureScheduleBatchAsync(trainerA.Id, clubA.Id, new Dictionary<int, string[]>
            {
                [0] = new[] { "08:00", "09:00", "10:00", "11:00", "14:00", "15:00", "18:00", "19:00", "20:00" },
                [1] = new[] { "09:00", "10:00", "13:00", "16:00", "18:00", "20:00" },
                [2] = new[] { "10:00", "11:00", "15:00", "16:00", "19:00" },
                [3] = new[] { "09:00", "11:00", "17:00", "18:00", "20:00" }
            }, now);

            await EnsureScheduleBatchAsync(trainerB.Id, clubA.Id, new Dictionary<int, string[]>
            {
                [0] = new[] { "10:00", "11:00", "16:00", "18:00", "20:00" },
                [1] = new[] { "08:00", "11:00", "14:00", "17:00", "19:00" },
                [2] = new[] { "09:00", "12:00", "15:00", "18:00" },
                [3] = new[] { "10:00", "13:00", "17:00", "20:00" }
            }, now);

            await EnsureMemberPackageAsync(memberA.Id, clubA.Id, "私教12节课包", "金卡会员", 12, 8, DateTime.Today.AddDays(-30), DateTime.Today.AddMonths(4), "Active", 10, now);
            await EnsureMemberPackageAsync(memberB.Id, clubB.Id, "私教8节体验包", "银卡会员", 8, 6, DateTime.Today.AddDays(-15), DateTime.Today.AddMonths(2), "Active", 20, now);

            var trainerAToday19 = await FindSlotAsync(trainerA.Id, DateTime.Today, "19:00", "20:00");
            var trainerBTomorrow10 = await FindSlotAsync(trainerB.Id, DateTime.Today.AddDays(1), "10:00", "11:00");
            var trainerADay215 = await FindSlotAsync(trainerA.Id, DateTime.Today.AddDays(2), "15:00", "16:00");
            var trainerBToday18 = await FindSlotAsync(trainerB.Id, DateTime.Today, "18:00", "19:00");

            var order1 = await EnsureReservationOrderAsync("RSV-DEMO-1001", memberA.Id, trainerA.Id, clubA.Id, trainerARegular.Id, trainerAToday19?.Id, DateTime.Today, "19:00", "20:00", 280m, "Upcoming", "会员希望加强核心稳定", 10, now);
            var order2 = await EnsureReservationOrderAsync("RSV-DEMO-1002", memberA.Id, trainerB.Id, clubA.Id, trainerBRegular.Id, trainerBTomorrow10?.Id, DateTime.Today.AddDays(1), "10:00", "11:00", 260m, "Completed", string.Empty, 20, now);
            var order3 = await EnsureReservationOrderAsync("RSV-DEMO-1003", memberB.Id, trainerA.Id, clubA.Id, trainerAAssessment.Id, trainerADay215?.Id, DateTime.Today.AddDays(2), "15:00", "16:00", 180m, "Cancelled", string.Empty, 30, now);
            var order4 = await EnsureReservationOrderAsync("RSV-DEMO-1004", memberB.Id, trainerB.Id, clubA.Id, trainerBStrength.Id, trainerBToday18?.Id, DateTime.Today, "18:00", "19:00", 260m, "Upcoming", "侧重下肢力量", 40, now);

            await EnsureReviewAsync(order2.Id, trainerB.Id, memberA.Id, "张小力", 4.9m, "动作反馈专业", "课程节奏舒服，动作讲解很清楚，训练后恢复也很好。", 10, now);
            await EnsureReviewAsync(order3.Id, trainerA.Id, memberB.Id, "林悦", 4.8m, "训练目标清晰", "会根据当日状态调整训练内容，整体体验很稳定。", 20, now);

            var pendingSlot = await FindSlotAsync(trainerA.Id, DateTime.Today.AddDays(3), "18:00", "19:00");
            await EnsureReservationOrderAsync("RSV-DEMO-1005", memberA.Id, trainerA.Id, clubA.Id, trainerABodyShape.Id, pendingSlot?.Id, DateTime.Today.AddDays(3), "18:00", "19:00", 280m, "Pending", "希望加练核心和下肢稳定", 50, now);

            var packageBasic = await EnsureCoursePackageAsync("PKG-BASIC-10", "私教基础课包（10节）", "私教课包", "适合建立训练习惯与标准动作模式的会员。", "https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=640&q=80", 2280m, 1680m, 10, 90, "初级 / 中级教练", "全城门店通用", true, 10, now);
            var packageAdvance = await EnsureCoursePackageAsync("PKG-ADV-20", "私教进阶课包（20节）", "私教课包", "适合已经有训练基础，想要稳定塑形与提升表现的会员。", "https://images.unsplash.com/photo-1571019613540-9967f3c2f4b3?auto=format&fit=crop&w=640&q=80", 4360m, 3180m, 20, 180, "中级 / 高级教练", "全城门店通用", true, 20, now);
            var packageShape = await EnsureCoursePackageAsync("PKG-SHAPE-30", "私教塑形强化课包（30节）", "私教课包", "适合有阶段目标，需要周期化跟进的深度会员。", "https://images.unsplash.com/photo-1518611012118-696072aa579a?auto=format&fit=crop&w=640&q=80", 6540m, 4580m, 30, 270, "高级教练", "全城门店通用", false, 30, now);

            var couponA = await EnsureCouponAsync("CPN-300", "满 2000 元可用", "Amount", 300m, 2000m, "适用：私教课包、体测课", DateTime.Today.AddDays(-5), DateTime.Today.AddDays(25), 10, now);
            var couponB = await EnsureCouponAsync("CPN-80P", "8 折券", "Discount", 0.8m, 1500m, "满 1500 元可用", DateTime.Today.AddDays(-3), DateTime.Today.AddDays(18), 20, now);
            var couponC = await EnsureCouponAsync("CPN-150", "新人券", "Amount", 150m, 999m, "满 999 元可用", DateTime.Today.AddDays(-7), DateTime.Today.AddDays(12), 30, now);

            var courseOrder1 = await EnsureCourseOrderAsync("202605171234567890", memberA.Id, packageBasic.Id, 1, 2280m, 300m, 80m, 1900m, couponA.Id, "微信支付", "PendingPayment", DateTime.Today.AddDays(-1).AddHours(15), null, null, "会员端下单", 10, now);
            await EnsureCourseOrderAsync("202605101020300001", memberA.Id, packageAdvance.Id, 1, 4360m, 980m, 200m, 3180m, couponB.Id, "支付宝", "Paid", DateTime.Today.AddDays(-8).AddHours(10), DateTime.Today.AddDays(-8).AddHours(10), DateTime.Today.AddDays(-8).AddHours(10), string.Empty, 20, now);
            await EnsureCourseOrderAsync("202605080915000012", memberB.Id, packageShape.Id, 1, 6540m, 1760m, 200m, 4580m, couponC.Id, "微信支付", "Completed", DateTime.Today.AddDays(-10).AddHours(9), DateTime.Today.AddDays(-10).AddHours(9), DateTime.Today.AddDays(-10).AddHours(9), string.Empty, 30, now);

            await EnsureMemberCouponAsync(memberA.Id, couponA.Id, courseOrder1.Id, "Available", now.AddDays(-2), null, 10, now);
            await EnsureMemberCouponAsync(memberA.Id, couponB.Id, null, "Available", now.AddDays(-1), null, 20, now);
            await EnsureMemberCouponAsync(memberB.Id, couponC.Id, null, "Available", now.AddDays(-4), null, 30, now);

            await EnsureBodyMetricAsync(memberA.Id, DateTime.Today.AddDays(-30).AddHours(9), 77.6m, 22.8m, 25.2m, 51.3m, 10, now);
            await EnsureBodyMetricAsync(memberA.Id, DateTime.Today.AddDays(-16).AddHours(9), 76.3m, 22.1m, 24.8m, 52.1m, 20, now);
            await EnsureBodyMetricAsync(memberA.Id, DateTime.Today.AddDays(-8).AddHours(9), 75.0m, 21.5m, 24.4m, 53.0m, 30, now);
            await EnsureBodyMetricAsync(memberA.Id, DateTime.Today.AddDays(-2).AddHours(9), 72.5m, 20.3m, 23.7m, 54.2m, 40, now);

            await EnsureTrainingRecordAsync(memberA.Id, coachB.Id, order2.Id, DateTime.Today.AddDays(-12).AddHours(10), "减脂塑形私教课", 60, 520, "动力健身（徐汇店）", "Completed", "动作控制稳定，建议继续加强下肢和核心。", "[{\"action\":\"深蹲\",\"sets\":4,\"reps\":12,\"weight\":\"60kg\"}]", 10, now);
            await EnsureTrainingRecordAsync(memberA.Id, coachB.Id, order1.Id, DateTime.Today.AddDays(-5).AddHours(19), "上肢力量训练", 60, 480, "动力健身（徐汇店）", "Completed", "肩胛稳定表现良好，上肢推拉动作节奏合适。", "[{\"action\":\"卧推\",\"sets\":4,\"reps\":10,\"weight\":\"40kg\"}]", 20, now);
            await EnsureTrainingRecordAsync(memberB.Id, coachA.Id, order4.Id, DateTime.Today.AddDays(-3).AddHours(18), "有氧燃脂训练", 45, 420, "动力健身（前海店）", "Completed", "心率控制平稳，耐力有提升。", "[{\"action\":\"划船机\",\"sets\":3,\"reps\":\"8min\",\"weight\":\"-\"}]", 30, now);

            var memberPlan = await EnsureTrainingPlanAsync(memberA.Id, coachB.Id, "4 周减脂塑形计划", "减脂塑形", DateTime.Today, DateTime.Today.AddDays(27), "4 / 7 完成", "Active", 10, now);
            await EnsureTrainingPlanItemAsync(memberPlan.Id, "周一", 10, "胸部 + 肩部训练", 60, 520, true, now);
            await EnsureTrainingPlanItemAsync(memberPlan.Id, "周三", 20, "腿部 + 背部训练", 60, 560, true, now);
            await EnsureTrainingPlanItemAsync(memberPlan.Id, "周五", 30, "背部 + 手臂训练", 30, 260, true, now);
            await EnsureTrainingPlanItemAsync(memberPlan.Id, "周六", 40, "有氧 + 核心训练", 45, 420, false, now);

            await EnsureConversationMessageAsync("member-01-coach-01", memberA.Id, coachA.Id, "member", "张小力", "教练您好，我今天可能会晚到一下下，多久下楼比较合适呀？", DateTime.Today.AddHours(18).AddMinutes(30), 10, now);
            await EnsureConversationMessageAsync("member-01-coach-01", memberA.Id, coachA.Id, "coach", "张伟", "可以的，没问题！提前 10 分钟到店就能顺利衔接。", DateTime.Today.AddHours(18).AddMinutes(32), 20, now);
            await EnsureConversationMessageAsync("member-01-coach-01", memberA.Id, coachA.Id, "member", "张小力", "好的。另外我下周想加一节课，周六下午方便吗？", DateTime.Today.AddHours(18).AddMinutes(33), 30, now);
            await EnsureConversationMessageAsync("member-01-coach-01", memberA.Id, coachA.Id, "coach", "张伟", "周六下午 15:00 有空，我先帮你预留。", DateTime.Today.AddHours(18).AddMinutes(35), 40, now);

            await EnsureCheckInRecordAsync(order2.Id, memberA.Id, coachB.Id, "扫码签到", DateTime.Today.AddDays(-8).AddHours(9).AddMinutes(42), clubA.ClubName, "私教训练区 A", "CheckedIn", 10, now);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ReservationDemoSeedResultDto
            {
                Seeded = true,
                Message = "预约演示数据已准备完成",
                ClubCount = await _context.ReservationClubs.CountAsync(),
                TrainerCount = await _context.ReservationTrainerProfiles.CountAsync(),
                PackageCount = await _context.ReservationMemberPackages.CountAsync(),
                ReservationCount = await _context.ReservationOrders.CountAsync(),
                Accounts = new List<ReservationDemoAccountDto>
                {
                    new ReservationDemoAccountDto { Role = "member", UserId = memberA.Id, UserName = memberA.UserName, Password = password, DisplayName = memberA.FullName ?? memberA.UserName },
                    new ReservationDemoAccountDto { Role = "member", UserId = memberB.Id, UserName = memberB.UserName, Password = password, DisplayName = memberB.FullName ?? memberB.UserName },
                    new ReservationDemoAccountDto { Role = "coach", UserId = coachA.Id, TrainerProfileId = trainerA.Id, UserName = coachA.UserName, Password = password, DisplayName = trainerA.DisplayName },
                    new ReservationDemoAccountDto { Role = "coach", UserId = coachB.Id, TrainerProfileId = trainerB.Id, UserName = coachB.UserName, Password = password, DisplayName = trainerB.DisplayName }
                }
            };
        }

        private async Task<ReservationClub> EnsureClubAsync(string clubCode, string clubName, string city, string district, string address, string businessHours, int seqNo, DateTime now)
        {
            var club = await _context.ReservationClubs.FirstOrDefaultAsync(item => item.ClubCode == clubCode);
            if (club == null)
            {
                club = new ReservationClub
                {
                    ClubCode = clubCode,
                    CrtTime = now
                };
                _context.ReservationClubs.Add(club);
            }

            club.ClubName = clubName;
            club.City = city;
            club.District = district;
            club.Address = address;
            club.BusinessHours = businessHours;
            club.IsActive = true;
            club.SeqNo = seqNo;
            club.UpdTime = now;
            await _context.SaveChangesAsync();
            return club;
        }

        private async Task<User> EnsureUserAsync(string userName, string fullName, string email, string phoneNumber, string password, string userTypeCode, string userToSystemCode, string avatarUrl, DateTime now)
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.UserName == userName);
            if (user == null)
            {
                var salt = GenerateSalt();
                user = new User
                {
                    UserName = userName,
                    Email = email,
                    Salt = salt,
                    PasswordHash = HashPassword(password, salt),
                    CrtTime = now
                };
                _context.Users.Add(user);
            }

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.ProfilePictureUrl = avatarUrl;
            user.IsActive = true;
            user.UserTypeCode = userTypeCode;
            user.UserToSystemCode = userToSystemCode;
            user.UpdTime = now;
            return user;
        }

        private async Task<ReservationTrainerProfile> EnsureTrainerProfileAsync(int userId, int clubId, string displayName, string title, string gender, int years, decimal rating, int reviewCount, int servedClients, int satisfaction, decimal basePrice, string trainingArea, string highlight, string introduction, string story, string heroImageUrl, string heroTone, string accentTone, bool isRecommended, int seqNo, DateTime now)
        {
            var profile = await _context.ReservationTrainerProfiles.FirstOrDefaultAsync(item => item.UserId == userId);
            if (profile == null)
            {
                profile = new ReservationTrainerProfile
                {
                    UserId = userId,
                    CrtTime = now
                };
                _context.ReservationTrainerProfiles.Add(profile);
            }

            profile.ClubId = clubId;
            profile.DisplayName = displayName;
            profile.Title = title;
            profile.Gender = gender;
            profile.YearsOfExperience = years;
            profile.Rating = rating;
            profile.ReviewCount = reviewCount;
            profile.ServedClients = servedClients;
            profile.Satisfaction = satisfaction;
            profile.BasePrice = basePrice;
            profile.TrainingArea = trainingArea;
            profile.Highlight = highlight;
            profile.Introduction = introduction;
            profile.Story = story;
            profile.HeroImageUrl = heroImageUrl;
            profile.HeroTone = heroTone;
            profile.AccentTone = accentTone;
            profile.IsRecommended = isRecommended;
            profile.IsActive = true;
            profile.SeqNo = seqNo;
            profile.UpdTime = now;
            await _context.SaveChangesAsync();
            return profile;
        }

        private async Task EnsureTrainerTagsAsync(int trainerProfileId, IEnumerable<(string TagType, string TagName)> tags, DateTime now)
        {
            var existing = await _context.ReservationTrainerTags.Where(item => item.TrainerProfileId == trainerProfileId).ToListAsync();
            _context.ReservationTrainerTags.RemoveRange(existing);
            await _context.SaveChangesAsync();

            var seqNo = 0;
            foreach (var (tagType, tagName) in tags)
            {
                seqNo += 10;
                _context.ReservationTrainerTags.Add(new ReservationTrainerTag
                {
                    TrainerProfileId = trainerProfileId,
                    TagType = tagType,
                    TagName = tagName,
                    SeqNo = seqNo,
                    CrtTime = now,
                    UpdTime = now
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task<ReservationTrainerSessionType> EnsureSessionTypeAsync(int trainerProfileId, string sessionCode, string sessionName, string description, int durationMinutes, decimal price, int seqNo, DateTime now)
        {
            var session = await _context.ReservationTrainerSessionTypes.FirstOrDefaultAsync(item => item.TrainerProfileId == trainerProfileId && item.SessionCode == sessionCode);
            if (session == null)
            {
                session = new ReservationTrainerSessionType
                {
                    TrainerProfileId = trainerProfileId,
                    SessionCode = sessionCode,
                    CrtTime = now
                };
                _context.ReservationTrainerSessionTypes.Add(session);
            }

            session.SessionName = sessionName;
            session.Description = description;
            session.DurationMinutes = durationMinutes;
            session.Price = price;
            session.IsActive = true;
            session.SeqNo = seqNo;
            session.UpdTime = now;
            await _context.SaveChangesAsync();
            return session;
        }

        private async Task EnsureScheduleBatchAsync(int trainerProfileId, int clubId, Dictionary<int, string[]> slotsByDayOffset, DateTime now)
        {
            foreach (var pair in slotsByDayOffset)
            {
                var date = DateTime.Today.AddDays(pair.Key);
                foreach (var startTime in pair.Value)
                {
                    var endTime = TimeSpan.Parse(startTime).Add(TimeSpan.FromHours(1)).ToString(@"hh\:mm");
                    var slot = await _context.ReservationTrainerScheduleSlots.FirstOrDefaultAsync(item =>
                        item.TrainerProfileId == trainerProfileId &&
                        item.ScheduleDate == date &&
                        item.StartTime == startTime &&
                        item.EndTime == endTime);

                    if (slot == null)
                    {
                        slot = new ReservationTrainerScheduleSlot
                        {
                            TrainerProfileId = trainerProfileId,
                            ScheduleDate = date,
                            StartTime = startTime,
                            EndTime = endTime,
                            CrtTime = now
                        };
                        _context.ReservationTrainerScheduleSlots.Add(slot);
                    }

                    slot.ClubId = clubId;
                    slot.IsAvailable = true;
                    slot.SeqNo = 0;
                    slot.UpdTime = now;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task EnsureMemberPackageAsync(int memberId, int clubId, string packageName, string membershipName, int totalSessions, int remainingSessions, DateTime effectiveDate, DateTime expireDate, string statusCode, int seqNo, DateTime now)
        {
            var package = await _context.ReservationMemberPackages.FirstOrDefaultAsync(item => item.MemberId == memberId && item.PackageName == packageName);
            if (package == null)
            {
                package = new ReservationMemberPackage
                {
                    MemberId = memberId,
                    PackageName = packageName,
                    CrtTime = now
                };
                _context.ReservationMemberPackages.Add(package);
            }

            package.ClubId = clubId;
            package.MembershipName = membershipName;
            package.TotalSessions = totalSessions;
            package.RemainingSessions = remainingSessions;
            package.EffectiveDate = effectiveDate.Date;
            package.ExpireDate = expireDate.Date;
            package.StatusCode = statusCode;
            package.SeqNo = seqNo;
            package.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task<ReservationOrder> EnsureReservationOrderAsync(string reservationNo, int memberId, int trainerProfileId, int clubId, int sessionTypeId, int? scheduleSlotId, DateTime reservationDate, string startTime, string endTime, decimal priceAmount, string statusCode, string remark, int seqNo, DateTime now)
        {
            var order = await _context.ReservationOrders.FirstOrDefaultAsync(item => item.ReservationNo == reservationNo);
            if (order == null)
            {
                order = new ReservationOrder
                {
                    ReservationNo = reservationNo,
                    CrtTime = now
                };
                _context.ReservationOrders.Add(order);
            }

            order.MemberId = memberId;
            order.TrainerProfileId = trainerProfileId;
            order.ClubId = clubId;
            order.SessionTypeId = sessionTypeId;
            order.ScheduleSlotId = scheduleSlotId;
            order.ReservationDate = reservationDate.Date;
            order.StartTime = startTime;
            order.EndTime = endTime;
            order.PriceAmount = priceAmount;
            order.StatusCode = statusCode;
            order.Remark = remark;
            order.CancelTime = statusCode == "Cancelled" ? now : null;
            order.CompleteTime = statusCode == "Completed" ? now : null;
            order.SeqNo = seqNo;
            order.UpdTime = now;
            await _context.SaveChangesAsync();

            if (scheduleSlotId.HasValue)
            {
                var slot = await _context.ReservationTrainerScheduleSlots.FirstOrDefaultAsync(item => item.Id == scheduleSlotId.Value);
                if (slot != null)
                {
                    slot.IsAvailable = statusCode == "Cancelled";
                    slot.UpdTime = now;
                    await _context.SaveChangesAsync();
                }
            }

            return order;
        }

        private async Task EnsureReviewAsync(int reservationOrderId, int trainerProfileId, int memberId, string authorName, decimal rating, string reviewTag, string content, int seqNo, DateTime now)
        {
            var review = await _context.ReservationReviews.FirstOrDefaultAsync(item => item.ReservationOrderId == reservationOrderId);
            if (review == null)
            {
                review = new ReservationReview
                {
                    ReservationOrderId = reservationOrderId,
                    CrtTime = now
                };
                _context.ReservationReviews.Add(review);
            }

            review.TrainerProfileId = trainerProfileId;
            review.MemberId = memberId;
            review.AuthorName = authorName;
            review.Rating = rating;
            review.ReviewTag = reviewTag;
            review.Content = content;
            review.IsVisible = true;
            review.SeqNo = seqNo;
            review.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task<ReservationCoursePackage> EnsureCoursePackageAsync(string packageCode, string packageName, string categoryName, string summary, string coverImageUrl, decimal originalPrice, decimal salePrice, int totalSessions, int validDays, string coachLevel, string clubScope, bool isRecommended, int seqNo, DateTime now)
        {
            var package = await _context.ReservationCoursePackages.FirstOrDefaultAsync(item => item.PackageCode == packageCode);
            if (package == null)
            {
                package = new ReservationCoursePackage
                {
                    PackageCode = packageCode,
                    CrtTime = now
                };
                _context.ReservationCoursePackages.Add(package);
            }

            package.PackageName = packageName;
            package.CategoryName = categoryName;
            package.Summary = summary;
            package.CoverImageUrl = coverImageUrl;
            package.OriginalPrice = originalPrice;
            package.SalePrice = salePrice;
            package.TotalSessions = totalSessions;
            package.ValidDays = validDays;
            package.CoachLevel = coachLevel;
            package.ClubScope = clubScope;
            package.IsRecommended = isRecommended;
            package.StatusCode = "Active";
            package.SeqNo = seqNo;
            package.UpdTime = now;
            await _context.SaveChangesAsync();
            return package;
        }

        private async Task<ReservationCoupon> EnsureCouponAsync(string couponCode, string title, string couponType, decimal discountValue, decimal minAmount, string ruleText, DateTime startDate, DateTime endDate, int seqNo, DateTime now)
        {
            var coupon = await _context.ReservationCoupons.FirstOrDefaultAsync(item => item.CouponCode == couponCode);
            if (coupon == null)
            {
                coupon = new ReservationCoupon
                {
                    CouponCode = couponCode,
                    CrtTime = now
                };
                _context.ReservationCoupons.Add(coupon);
            }

            coupon.Title = title;
            coupon.CouponType = couponType;
            coupon.DiscountValue = discountValue;
            coupon.MinAmount = minAmount;
            coupon.RuleText = ruleText;
            coupon.StartDate = startDate.Date;
            coupon.EndDate = endDate.Date;
            coupon.IsActive = true;
            coupon.SeqNo = seqNo;
            coupon.UpdTime = now;
            await _context.SaveChangesAsync();
            return coupon;
        }

        private async Task<ReservationCourseOrder> EnsureCourseOrderAsync(string orderNo, int memberId, int coursePackageId, int quantity, decimal originAmount, decimal discountAmount, decimal pointDiscountAmount, decimal payAmount, int? couponId, string paymentMethod, string statusCode, DateTime orderTime, DateTime? payTime, DateTime? effectiveTime, string remark, int seqNo, DateTime now)
        {
            var order = await _context.ReservationCourseOrders.FirstOrDefaultAsync(item => item.OrderNo == orderNo);
            if (order == null)
            {
                order = new ReservationCourseOrder
                {
                    OrderNo = orderNo,
                    CrtTime = now
                };
                _context.ReservationCourseOrders.Add(order);
            }

            order.MemberId = memberId;
            order.CoursePackageId = coursePackageId;
            order.Quantity = quantity;
            order.OriginAmount = originAmount;
            order.DiscountAmount = discountAmount;
            order.PointDiscountAmount = pointDiscountAmount;
            order.PayAmount = payAmount;
            order.CouponId = couponId;
            order.PaymentMethod = paymentMethod;
            order.StatusCode = statusCode;
            order.OrderTime = orderTime;
            order.PayTime = payTime;
            order.EffectiveTime = effectiveTime;
            order.Remark = remark;
            order.SeqNo = seqNo;
            order.UpdTime = now;
            await _context.SaveChangesAsync();
            return order;
        }

        private async Task EnsureMemberCouponAsync(int memberId, int couponId, int? courseOrderId, string statusCode, DateTime claimedTime, DateTime? usedTime, int seqNo, DateTime now)
        {
            var memberCoupon = await _context.ReservationMemberCoupons.FirstOrDefaultAsync(item => item.MemberId == memberId && item.CouponId == couponId);
            if (memberCoupon == null)
            {
                memberCoupon = new ReservationMemberCoupon
                {
                    MemberId = memberId,
                    CouponId = couponId,
                    CrtTime = now
                };
                _context.ReservationMemberCoupons.Add(memberCoupon);
            }

            memberCoupon.CourseOrderId = courseOrderId;
            memberCoupon.StatusCode = statusCode;
            memberCoupon.ClaimedTime = claimedTime;
            memberCoupon.UsedTime = usedTime;
            memberCoupon.SeqNo = seqNo;
            memberCoupon.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task EnsureBodyMetricAsync(int memberId, DateTime measureTime, decimal weightKg, decimal bodyFatRate, decimal bmi, decimal muscleKg, int seqNo, DateTime now)
        {
            var metric = await _context.ReservationBodyMetrics.FirstOrDefaultAsync(item => item.MemberId == memberId && item.MeasureTime == measureTime);
            if (metric == null)
            {
                metric = new ReservationBodyMetric
                {
                    MemberId = memberId,
                    MeasureTime = measureTime,
                    CrtTime = now
                };
                _context.ReservationBodyMetrics.Add(metric);
            }

            metric.WeightKg = weightKg;
            metric.BodyFatRate = bodyFatRate;
            metric.Bmi = bmi;
            metric.MuscleKg = muscleKg;
            metric.SeqNo = seqNo;
            metric.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task EnsureTrainingRecordAsync(int memberId, int coachUserId, int? reservationOrderId, DateTime recordTime, string title, int durationMinutes, int calories, string locationName, string statusCode, string summary, string actionNotesJson, int seqNo, DateTime now)
        {
            var record = await _context.ReservationTrainingRecords.FirstOrDefaultAsync(item => item.MemberId == memberId && item.RecordTime == recordTime && item.Title == title);
            if (record == null)
            {
                record = new ReservationTrainingRecord
                {
                    MemberId = memberId,
                    RecordTime = recordTime,
                    Title = title,
                    CrtTime = now
                };
                _context.ReservationTrainingRecords.Add(record);
            }

            record.CoachUserId = coachUserId;
            record.ReservationOrderId = reservationOrderId;
            record.DurationMinutes = durationMinutes;
            record.Calories = calories;
            record.LocationName = locationName;
            record.StatusCode = statusCode;
            record.Summary = summary;
            record.ActionNotesJson = actionNotesJson;
            record.SeqNo = seqNo;
            record.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task<ReservationTrainingPlan> EnsureTrainingPlanAsync(int memberId, int? coachUserId, string planName, string goal, DateTime startDate, DateTime endDate, string progressText, string statusCode, int seqNo, DateTime now)
        {
            var plan = await _context.ReservationTrainingPlans.FirstOrDefaultAsync(item => item.MemberId == memberId && item.PlanName == planName);
            if (plan == null)
            {
                plan = new ReservationTrainingPlan
                {
                    MemberId = memberId,
                    PlanName = planName,
                    CrtTime = now
                };
                _context.ReservationTrainingPlans.Add(plan);
            }

            plan.CoachUserId = coachUserId;
            plan.Goal = goal;
            plan.StartDate = startDate.Date;
            plan.EndDate = endDate.Date;
            plan.ProgressText = progressText;
            plan.StatusCode = statusCode;
            plan.SeqNo = seqNo;
            plan.UpdTime = now;
            await _context.SaveChangesAsync();
            return plan;
        }

        private async Task EnsureTrainingPlanItemAsync(int planId, string dayLabel, int sortOrder, string title, int durationMinutes, int caloriesTarget, bool isCompleted, DateTime now)
        {
            var item = await _context.ReservationTrainingPlanItems.FirstOrDefaultAsync(entry => entry.PlanId == planId && entry.DayLabel == dayLabel);
            if (item == null)
            {
                item = new ReservationTrainingPlanItem
                {
                    PlanId = planId,
                    DayLabel = dayLabel,
                    CrtTime = now
                };
                _context.ReservationTrainingPlanItems.Add(item);
            }

            item.SortOrder = sortOrder;
            item.Title = title;
            item.DurationMinutes = durationMinutes;
            item.CaloriesTarget = caloriesTarget;
            item.IsCompleted = isCompleted;
            item.SeqNo = sortOrder;
            item.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task EnsureConversationMessageAsync(string sessionKey, int memberId, int coachUserId, string senderRole, string senderName, string content, DateTime sentTime, int seqNo, DateTime now)
        {
            var message = await _context.ReservationConversationMessages.FirstOrDefaultAsync(item => item.SessionKey == sessionKey && item.SentTime == sentTime && item.SenderRole == senderRole);
            if (message == null)
            {
                message = new ReservationConversationMessage
                {
                    SessionKey = sessionKey,
                    SentTime = sentTime,
                    SenderRole = senderRole,
                    CrtTime = now
                };
                _context.ReservationConversationMessages.Add(message);
            }

            message.MemberId = memberId;
            message.CoachUserId = coachUserId;
            message.SenderName = senderName;
            message.Content = content;
            message.StatusCode = "Sent";
            message.SeqNo = seqNo;
            message.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task EnsureCheckInRecordAsync(int reservationOrderId, int memberId, int coachUserId, string checkInMethod, DateTime checkInTime, string clubName, string areaName, string statusCode, int seqNo, DateTime now)
        {
            var record = await _context.ReservationCheckInRecords.FirstOrDefaultAsync(item => item.ReservationOrderId == reservationOrderId);
            if (record == null)
            {
                record = new ReservationCheckInRecord
                {
                    ReservationOrderId = reservationOrderId,
                    CrtTime = now
                };
                _context.ReservationCheckInRecords.Add(record);
            }

            record.MemberId = memberId;
            record.CoachUserId = coachUserId;
            record.CheckInMethod = checkInMethod;
            record.CheckInTime = checkInTime;
            record.ClubName = clubName;
            record.AreaName = areaName;
            record.StatusCode = statusCode;
            record.SeqNo = seqNo;
            record.UpdTime = now;
            await _context.SaveChangesAsync();
        }

        private async Task<ReservationTrainerScheduleSlot?> FindSlotAsync(int trainerProfileId, DateTime scheduleDate, string startTime, string endTime)
        {
            return await _context.ReservationTrainerScheduleSlots.FirstOrDefaultAsync(item =>
                item.TrainerProfileId == trainerProfileId &&
                item.ScheduleDate == scheduleDate.Date &&
                item.StartTime == startTime &&
                item.EndTime == endTime);
        }

        private static string GenerateSalt()
        {
            var bytes = new byte[16];
            using var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
