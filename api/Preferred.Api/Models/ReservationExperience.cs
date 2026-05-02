using System.Collections.Generic;

namespace Preferred.Api.Models
{
    public class ReservationCoachDashboardDto
    {
        public int CoachUserId { get; set; }
        public int TrainerProfileId { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public int TodayReservationCount { get; set; }
        public int UpcomingReservationCount { get; set; }
        public int CompletedReservationCount { get; set; }
        public int BoundMemberCount { get; set; }
        public List<ReservationCoachReservationDto> TodayReservations { get; set; } = new List<ReservationCoachReservationDto>();
    }

    public class ReservationCoachReservationDto
    {
        public int Id { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MemberAvatarUrl { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
        public string ReservationDate { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }

    public class ReservationCoachMemberDto
    {
        public int MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string MembershipName { get; set; } = string.Empty;
        public int RemainingSessions { get; set; }
        public string LatestReservation { get; set; } = string.Empty;
    }

    public class ReservationCoachScheduleDto
    {
        public string ScheduleDate { get; set; } = string.Empty;
        public List<ReservationCoachScheduleItemDto> Items { get; set; } = new List<ReservationCoachScheduleItemDto>();
    }

    public class ReservationCoachScheduleItemDto
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsReserved { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
    }

    public class ReservationCoachProfileDto
    {
        public int CoachUserId { get; set; }
        public int TrainerProfileId { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Highlight { get; set; } = string.Empty;
    }

    public class ReservationDemoAccountDto
    {
        public string Role { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int? TrainerProfileId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class ReservationDemoSeedResultDto
    {
        public bool Seeded { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ClubCount { get; set; }
        public int TrainerCount { get; set; }
        public int PackageCount { get; set; }
        public int ReservationCount { get; set; }
        public List<ReservationDemoAccountDto> Accounts { get; set; } = new List<ReservationDemoAccountDto>();
    }
}
