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
    public class ReservationAppService : IReservationAppService
    {
        private readonly ApplicationDbContext _context;

        public ReservationAppService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReservationClubDto>> GetClubsAsync(string? city = null)
        {
            var query = _context.ReservationClubs
                .AsNoTracking()
                .Where(item => item.IsActive);

            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(item => item.City == city);
            }

            return await query
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .Select(item => new ReservationClubDto
                {
                    Id = item.Id,
                    ClubCode = item.ClubCode,
                    ClubName = item.ClubName,
                    City = item.City,
                    District = item.District,
                    Address = item.Address,
                    BusinessHours = item.BusinessHours
                })
                .ToListAsync();
        }

        public async Task<ReservationHomeDto?> GetHomeAsync(int memberId)
        {
            var summary = await BuildMemberSummaryAsync(memberId);
            if (summary == null)
            {
                return null;
            }

            var recommendedProfiles = await _context.ReservationTrainerProfiles
                .AsNoTracking()
                .Where(item => item.IsActive && item.IsRecommended)
                .OrderByDescending(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .Take(2)
                .ToListAsync();

            var recommended = await BuildTrainerCardsAsync(recommendedProfiles);

            var nextOrder = await _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.MemberId == memberId && item.StatusCode == "Upcoming")
                .OrderBy(item => item.ReservationDate)
                .ThenBy(item => item.StartTime)
                .FirstOrDefaultAsync();

            ReservationOrderItemDto? nextReservation = null;
            if (nextOrder != null)
            {
                nextReservation = (await BuildReservationItemsAsync(new List<ReservationOrder> { nextOrder })).FirstOrDefault();
            }

            return new ReservationHomeDto
            {
                User = summary,
                NextReservation = nextReservation,
                RecommendedTrainers = recommended
            };
        }

        public async Task<List<ReservationTrainerCardDto>> GetTrainersAsync(ReservationTrainerQuery query)
        {
            query ??= new ReservationTrainerQuery();

            var trainerQuery = _context.ReservationTrainerProfiles
                .AsNoTracking()
                .Where(item => item.IsActive);

            if (query.ClubId.HasValue)
            {
                trainerQuery = trainerQuery.Where(item => item.ClubId == query.ClubId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Gender))
            {
                trainerQuery = trainerQuery.Where(item => item.Gender == query.Gender);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                trainerQuery = trainerQuery.Where(item =>
                    item.DisplayName.Contains(keyword) ||
                    item.Title.Contains(keyword) ||
                    (item.Highlight != null && item.Highlight.Contains(keyword)) ||
                    (item.Introduction != null && item.Introduction.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(query.Goal))
            {
                var trainerIds = await _context.ReservationTrainerTags
                    .AsNoTracking()
                    .Where(item => item.TagType == "Goal" && item.TagName == query.Goal)
                    .Select(item => item.TrainerProfileId)
                    .Distinct()
                    .ToListAsync();

                trainerQuery = trainerQuery.Where(item => trainerIds.Contains(item.Id));
            }

            trainerQuery = ApplyTrainerSort(trainerQuery, query.SortBy);

            var trainers = await trainerQuery.ToListAsync();
            return await BuildTrainerCardsAsync(trainers);
        }

        public async Task<ReservationTrainerDetailDto?> GetTrainerDetailAsync(int trainerId)
        {
            var profile = await _context.ReservationTrainerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == trainerId && item.IsActive);

            if (profile == null)
            {
                return null;
            }

            return await BuildTrainerDetailAsync(profile);
        }

        public async Task<ReservationBookingPageDto?> GetBookingPageAsync(int trainerId, int memberId)
        {
            var detail = await GetTrainerDetailAsync(trainerId);
            if (detail == null)
            {
                return null;
            }

            var package = await GetActiveMemberPackageAsync(memberId);
            return new ReservationBookingPageDto
            {
                Trainer = detail,
                RemainingSessions = package?.RemainingSessions ?? 0
            };
        }

        public async Task<ReservationCreateResultDto?> CreateReservationAsync(ReservationCreateRequest request)
        {
            var profile = await _context.ReservationTrainerProfiles
                .FirstOrDefaultAsync(item => item.Id == request.TrainerId && item.IsActive);
            if (profile == null)
            {
                return null;
            }

            var sessionType = await _context.ReservationTrainerSessionTypes
                .FirstOrDefaultAsync(item => item.Id == request.SessionTypeId && item.TrainerProfileId == request.TrainerId && item.IsActive);
            if (sessionType == null)
            {
                return null;
            }

            var package = await GetActiveMemberPackageAsync(request.MemberId);
            if (package == null || package.RemainingSessions <= 0)
            {
                return null;
            }

            if (!DateTime.TryParseExact(request.ReservationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var reservationDate))
            {
                return null;
            }

            if (!TimeSpan.TryParse(request.StartTime, out var startTime))
            {
                return null;
            }

            var endTime = startTime.Add(TimeSpan.FromMinutes(sessionType.DurationMinutes));
            var startText = startTime.ToString(@"hh\:mm");
            var endText = endTime.ToString(@"hh\:mm");

            var slot = await _context.ReservationTrainerScheduleSlots
                .FirstOrDefaultAsync(item =>
                    item.TrainerProfileId == request.TrainerId &&
                    item.ScheduleDate == reservationDate &&
                    item.StartTime == startText &&
                    item.EndTime == endText &&
                    item.IsAvailable);
            if (slot == null)
            {
                return null;
            }

            var now = DateTime.Now;
            var reservationNo = $"RSV{now:yyyyMMddHHmmssfff}";

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var order = new ReservationOrder
            {
                ReservationNo = reservationNo,
                MemberId = request.MemberId,
                TrainerProfileId = request.TrainerId,
                ClubId = profile.ClubId,
                SessionTypeId = sessionType.Id,
                ScheduleSlotId = slot.Id,
                ReservationDate = reservationDate,
                StartTime = startText,
                EndTime = endText,
                PriceAmount = sessionType.Price,
                StatusCode = "Upcoming",
                Remark = request.Remark?.Trim(),
                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            };

            _context.ReservationOrders.Add(order);

            slot.IsAvailable = false;
            slot.UpdTime = now;

            package.RemainingSessions -= 1;
            package.UpdTime = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ReservationCreateResultDto
            {
                ReservationId = order.Id,
                ReservationNo = order.ReservationNo,
                RemainingSessions = package.RemainingSessions
            };
        }

        public async Task<List<ReservationOrderItemDto>> GetMemberReservationsAsync(int memberId, string? status = null)
        {
            var query = _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.MemberId == memberId);

            var normalizedStatus = NormalizeStatusCode(status);
            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                query = query.Where(item => item.StatusCode == normalizedStatus);
            }

            var items = await query
                .OrderBy(item => item.StatusCode == "Upcoming" ? 0 : item.StatusCode == "Completed" ? 1 : 2)
                .ThenBy(item => item.ReservationDate)
                .ThenBy(item => item.StartTime)
                .ToListAsync();

            return await BuildReservationItemsAsync(items);
        }

        public async Task<bool> CancelReservationAsync(ReservationCancelRequest request)
        {
            var order = await _context.ReservationOrders
                .FirstOrDefaultAsync(item => item.Id == request.ReservationId && item.MemberId == request.MemberId);
            if (order == null || order.StatusCode != "Upcoming")
            {
                return false;
            }

            var now = DateTime.Now;
            order.StatusCode = "Cancelled";
            order.CancelTime = now;
            order.UpdTime = now;

            if (order.ScheduleSlotId.HasValue)
            {
                var slot = await _context.ReservationTrainerScheduleSlots
                    .FirstOrDefaultAsync(item => item.Id == order.ScheduleSlotId.Value);
                if (slot != null)
                {
                    slot.IsAvailable = true;
                    slot.UpdTime = now;
                }
            }

            var package = await GetActiveMemberPackageAsync(request.MemberId);
            if (package != null)
            {
                package.RemainingSessions += 1;
                package.UpdTime = now;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ReservationProfileDto?> GetProfileAsync(int memberId)
        {
            var summary = await BuildMemberSummaryAsync(memberId);
            if (summary == null)
            {
                return null;
            }

            var orderGroups = await _context.ReservationOrders
                .AsNoTracking()
                .Where(item => item.MemberId == memberId)
                .GroupBy(item => item.StatusCode)
                .Select(group => new
                {
                    StatusCode = group.Key,
                    Count = group.Count()
                })
                .ToListAsync();

            return new ReservationProfileDto
            {
                User = summary,
                UpcomingCount = orderGroups.FirstOrDefault(item => item.StatusCode == "Upcoming")?.Count ?? 0,
                CompletedCount = orderGroups.FirstOrDefault(item => item.StatusCode == "Completed")?.Count ?? 0,
                CancelledCount = orderGroups.FirstOrDefault(item => item.StatusCode == "Cancelled")?.Count ?? 0
            };
        }

        private IQueryable<ReservationTrainerProfile> ApplyTrainerSort(IQueryable<ReservationTrainerProfile> query, string? sortBy)
        {
            return (sortBy ?? string.Empty).ToLowerInvariant() switch
            {
                "price" => query.OrderBy(item => item.BasePrice).ThenByDescending(item => item.Rating),
                "rating" => query.OrderByDescending(item => item.Rating).ThenByDescending(item => item.ReviewCount),
                "experience" => query.OrderByDescending(item => item.YearsOfExperience).ThenByDescending(item => item.Rating),
                _ => query.OrderByDescending(item => item.IsRecommended).ThenByDescending(item => item.SeqNo).ThenByDescending(item => item.Rating)
            };
        }

        private async Task<ReservationMemberPackage?> GetActiveMemberPackageAsync(int memberId)
        {
            var today = DateTime.Today;
            return await _context.ReservationMemberPackages
                .FirstOrDefaultAsync(item =>
                    item.MemberId == memberId &&
                    item.StatusCode == "Active" &&
                    item.EffectiveDate <= today &&
                    item.ExpireDate >= today);
        }

        private async Task<ReservationMemberSummaryDto?> BuildMemberSummaryAsync(int memberId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == memberId);
            if (user == null)
            {
                return null;
            }

            var package = await GetActiveMemberPackageAsync(memberId);
            var club = package == null
                ? null
                : await _context.ReservationClubs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == package.ClubId);

            return new ReservationMemberSummaryDto
            {
                MemberId = user.Id,
                Name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName,
                City = club?.City ?? string.Empty,
                Membership = package?.MembershipName ?? package?.PackageName ?? "普通会员",
                HomeClub = club?.ClubName ?? string.Empty,
                RemainingSessions = package?.RemainingSessions ?? 0,
                ExpireAt = package?.ExpireDate.ToString("yyyy-MM-dd") ?? string.Empty,
                AvatarUrl = user.ProfilePictureUrl
            };
        }

        private async Task<List<ReservationTrainerCardDto>> BuildTrainerCardsAsync(List<ReservationTrainerProfile> profiles)
        {
            if (profiles.Count == 0)
            {
                return new List<ReservationTrainerCardDto>();
            }

            var trainerIds = profiles.Select(item => item.Id).Distinct().ToList();
            var userIds = profiles.Select(item => item.UserId).Distinct().ToList();
            var clubIds = profiles.Select(item => item.ClubId).Distinct().ToList();

            var users = await _context.Users
                .AsNoTracking()
                .Where(item => userIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var clubs = await _context.ReservationClubs
                .AsNoTracking()
                .Where(item => clubIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var tags = await _context.ReservationTrainerTags
                .AsNoTracking()
                .Where(item => trainerIds.Contains(item.TrainerProfileId))
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .ToListAsync();

            var slots = await _context.ReservationTrainerScheduleSlots
                .AsNoTracking()
                .Where(item => trainerIds.Contains(item.TrainerProfileId) && item.IsAvailable && item.ScheduleDate >= DateTime.Today)
                .OrderBy(item => item.ScheduleDate)
                .ThenBy(item => item.StartTime)
                .ToListAsync();

            var tagLookup = tags.GroupBy(item => item.TrainerProfileId).ToDictionary(group => group.Key, group => group.ToList());
            var slotLookup = slots.GroupBy(item => item.TrainerProfileId).ToDictionary(group => group.Key, group => group.ToList());

            return profiles.Select(profile =>
            {
                users.TryGetValue(profile.UserId, out var user);
                clubs.TryGetValue(profile.ClubId, out var club);
                tagLookup.TryGetValue(profile.Id, out var trainerTags);
                slotLookup.TryGetValue(profile.Id, out var trainerSlots);

                return new ReservationTrainerCardDto
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    Name = ResolveTrainerName(profile, user),
                    Title = profile.Title,
                    PhotoUrl = profile.HeroImageUrl ?? user?.ProfilePictureUrl ?? string.Empty,
                    Rating = profile.Rating,
                    ReviewCount = profile.ReviewCount,
                    Years = profile.YearsOfExperience,
                    ServedClients = profile.ServedClients,
                    Satisfaction = profile.Satisfaction,
                    Price = profile.BasePrice,
                    Club = club?.ClubName ?? string.Empty,
                    Area = profile.TrainingArea ?? string.Empty,
                    Gender = profile.Gender,
                    Highlight = profile.Highlight ?? string.Empty,
                    HeroTone = profile.HeroTone ?? "#eaf7ef",
                    AccentTone = profile.AccentTone ?? "#16a34a",
                    Goals = ExtractTagNames(trainerTags, "Goal"),
                    Specialties = ExtractTagNames(trainerTags, "Specialty"),
                    Badges = ExtractTagNames(trainerTags, "Badge"),
                    NextSlots = (trainerSlots ?? new List<ReservationTrainerScheduleSlot>())
                        .Take(3)
                        .Select(item => BuildNextSlotLabel(item.ScheduleDate, item.StartTime))
                        .ToList()
                };
            }).ToList();
        }

        private async Task<ReservationTrainerDetailDto?> BuildTrainerDetailAsync(ReservationTrainerProfile profile)
        {
            var card = (await BuildTrainerCardsAsync(new List<ReservationTrainerProfile> { profile })).FirstOrDefault();
            if (card == null)
            {
                return null;
            }

            var tags = await _context.ReservationTrainerTags
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == profile.Id)
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .ToListAsync();

            var sessions = await _context.ReservationTrainerSessionTypes
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == profile.Id && item.IsActive)
                .OrderBy(item => item.SeqNo)
                .ThenBy(item => item.Id)
                .ToListAsync();

            var reviews = await _context.ReservationReviews
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == profile.Id && item.IsVisible)
                .OrderByDescending(item => item.CrtTime)
                .ThenByDescending(item => item.Id)
                .Take(8)
                .ToListAsync();

            return new ReservationTrainerDetailDto
            {
                Id = card.Id,
                UserId = card.UserId,
                Name = card.Name,
                Title = card.Title,
                PhotoUrl = card.PhotoUrl,
                Rating = card.Rating,
                ReviewCount = card.ReviewCount,
                Years = card.Years,
                ServedClients = card.ServedClients,
                Satisfaction = card.Satisfaction,
                Price = card.Price,
                Club = card.Club,
                Area = card.Area,
                Gender = card.Gender,
                Highlight = card.Highlight,
                HeroTone = card.HeroTone,
                AccentTone = card.AccentTone,
                Goals = card.Goals,
                Specialties = card.Specialties,
                Badges = card.Badges,
                NextSlots = card.NextSlots,
                Introduction = profile.Introduction ?? string.Empty,
                Story = profile.Story ?? string.Empty,
                Certifications = ExtractTagNames(tags, "Certification"),
                SessionTypes = sessions.Select(item => new ReservationSessionTypeDto
                {
                    Id = item.Id,
                    Code = item.SessionCode,
                    Label = item.SessionName,
                    Description = item.Description ?? string.Empty,
                    Price = item.Price,
                    DurationMinutes = item.DurationMinutes
                }).ToList(),
                AvailableDates = await BuildAvailableDatesAsync(profile.Id),
                Reviews = reviews.Select(item => new ReservationReviewDto
                {
                    Id = item.Id,
                    Author = item.AuthorName,
                    Rating = item.Rating,
                    Tag = item.ReviewTag ?? string.Empty,
                    Content = item.Content
                }).ToList()
            };
        }

        private async Task<List<ReservationDateSlotDto>> BuildAvailableDatesAsync(int trainerId)
        {
            var slots = await _context.ReservationTrainerScheduleSlots
                .AsNoTracking()
                .Where(item => item.TrainerProfileId == trainerId && item.IsAvailable && item.ScheduleDate >= DateTime.Today)
                .OrderBy(item => item.ScheduleDate)
                .ThenBy(item => item.StartTime)
                .ToListAsync();

            var groups = slots
                .GroupBy(item => item.ScheduleDate.Date)
                .OrderBy(group => group.Key)
                .ToList();

            var result = groups
                .Take(4)
                .Select(group => new ReservationDateSlotDto
                {
                    Key = group.Key.ToString("yyyy-MM-dd"),
                    Label = BuildDateLabel(group.Key),
                    SubLabel = group.Key.ToString("MM-dd"),
                    Times = group.Select(item => item.StartTime).Distinct().ToList()
                })
                .ToList();

            if (groups.Count > 4 && result.Count > 0)
            {
                result[^1].MoreLabel = "更多";
            }

            return result;
        }

        private async Task<List<ReservationOrderItemDto>> BuildReservationItemsAsync(List<ReservationOrder> orders)
        {
            if (orders.Count == 0)
            {
                return new List<ReservationOrderItemDto>();
            }

            var trainerIds = orders.Select(item => item.TrainerProfileId).Distinct().ToList();
            var sessionTypeIds = orders.Select(item => item.SessionTypeId).Distinct().ToList();
            var clubIds = orders.Select(item => item.ClubId).Distinct().ToList();

            var profiles = await _context.ReservationTrainerProfiles
                .AsNoTracking()
                .Where(item => trainerIds.Contains(item.Id))
                .ToListAsync();

            var users = await _context.Users
                .AsNoTracking()
                .Where(item => profiles.Select(profile => profile.UserId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var clubs = await _context.ReservationClubs
                .AsNoTracking()
                .Where(item => clubIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var sessionTypes = await _context.ReservationTrainerSessionTypes
                .AsNoTracking()
                .Where(item => sessionTypeIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            var profileMap = profiles.ToDictionary(item => item.Id);

            return orders.Select(item =>
            {
                profileMap.TryGetValue(item.TrainerProfileId, out var profile);
                ReservationClub? club = null;
                if (clubs.ContainsKey(item.ClubId))
                {
                    club = clubs[item.ClubId];
                }

                ReservationTrainerSessionType? sessionType = null;
                if (sessionTypes.ContainsKey(item.SessionTypeId))
                {
                    sessionType = sessionTypes[item.SessionTypeId];
                }

                User? user = null;
                if (profile != null && users.ContainsKey(profile.UserId))
                {
                    user = users[profile.UserId];
                }

                return new ReservationOrderItemDto
                {
                    Id = item.Id,
                    ReservationNo = item.ReservationNo,
                    TrainerId = item.TrainerProfileId,
                    TrainerName = profile == null ? string.Empty : ResolveTrainerName(profile, user),
                    TrainerPhotoUrl = profile?.HeroImageUrl ?? user?.ProfilePictureUrl ?? string.Empty,
                    SessionTypeId = item.SessionTypeId,
                    SessionLabel = sessionType?.SessionName ?? string.Empty,
                    DateLabel = BuildDateLabel(item.ReservationDate),
                    CalendarDate = item.ReservationDate.ToString("yyyy-MM-dd"),
                    TimeRange = $"{item.StartTime} - {item.EndTime}",
                    Club = club?.ClubName ?? string.Empty,
                    Area = profile?.TrainingArea ?? string.Empty,
                    Status = BuildClientStatus(item.StatusCode),
                    Tag = BuildStatusTag(item.StatusCode),
                    Note = item.Remark ?? string.Empty,
                    Price = item.PriceAmount
                };
            }).ToList();
        }

        private static List<string> ExtractTagNames(List<ReservationTrainerTag>? tags, string tagType)
        {
            if (tags == null)
            {
                return new List<string>();
            }

            return tags
                .Where(item => item.TagType == tagType)
                .Select(item => item.TagName)
                .Distinct()
                .ToList();
        }

        private static string ResolveTrainerName(ReservationTrainerProfile profile, User? user)
        {
            if (!string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                return profile.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(user?.FullName))
            {
                return user.FullName;
            }

            return user?.UserName ?? string.Empty;
        }

        private static string BuildNextSlotLabel(DateTime date, string startTime)
        {
            return $"{BuildDateLabel(date)} {startTime}";
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
                _ => date.ToString("MM-dd")
            };
        }

        private static string NormalizeStatusCode(string? status)
        {
            return (status ?? string.Empty).ToLowerInvariant() switch
            {
                "upcoming" => "Upcoming",
                "completed" => "Completed",
                "cancelled" => "Cancelled",
                "pending" => "Pending",
                "upcomingstatus" => "Upcoming",
                _ when string.IsNullOrWhiteSpace(status) => string.Empty,
                _ => status!
            };
        }

        private static string BuildClientStatus(string statusCode)
        {
            return statusCode switch
            {
                "Completed" => "completed",
                "Cancelled" => "cancelled",
                _ => "upcoming"
            };
        }

        private static string BuildStatusTag(string statusCode)
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
