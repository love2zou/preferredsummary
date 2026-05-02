using System;
using System.Collections.Generic;

namespace Preferred.Api.Models
{
    public class ReservationClub
    {
        public int Id { get; set; }
        public string ClubCode { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? BusinessHours { get; set; }
        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationTrainerProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ClubId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int YearsOfExperience { get; set; }
        public decimal Rating { get; set; }
        public int ReviewCount { get; set; }
        public int ServedClients { get; set; }
        public int Satisfaction { get; set; }
        public decimal BasePrice { get; set; }
        public string? TrainingArea { get; set; }
        public string? Highlight { get; set; }
        public string? Introduction { get; set; }
        public string? Story { get; set; }
        public string? HeroImageUrl { get; set; }
        public string? HeroTone { get; set; }
        public string? AccentTone { get; set; }
        public bool IsRecommended { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationTrainerTag
    {
        public int Id { get; set; }
        public int TrainerProfileId { get; set; }
        public string TagType { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationTrainerSessionType
    {
        public int Id { get; set; }
        public int TrainerProfileId { get; set; }
        public string SessionCode { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationTrainerScheduleSlot
    {
        public int Id { get; set; }
        public int TrainerProfileId { get; set; }
        public int ClubId { get; set; }
        public DateTime ScheduleDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationMemberPackage
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int ClubId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string? MembershipName { get; set; }
        public int TotalSessions { get; set; }
        public int RemainingSessions { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime ExpireDate { get; set; }
        public string StatusCode { get; set; } = "Active";
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationOrder
    {
        public int Id { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public int TrainerProfileId { get; set; }
        public int ClubId { get; set; }
        public int SessionTypeId { get; set; }
        public int? ScheduleSlotId { get; set; }
        public DateTime ReservationDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public decimal PriceAmount { get; set; }
        public string StatusCode { get; set; } = "Upcoming";
        public string? Remark { get; set; }
        public DateTime? CancelTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationReview
    {
        public int Id { get; set; }
        public int ReservationOrderId { get; set; }
        public int TrainerProfileId { get; set; }
        public int MemberId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public string? ReviewTag { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationTrainerQuery
    {
        public int? ClubId { get; set; }
        public string? Goal { get; set; }
        public string? Gender { get; set; }
        public string? Keyword { get; set; }
        public string? SortBy { get; set; }
    }

    public class ReservationClubDto
    {
        public int Id { get; set; }
        public string ClubCode { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? BusinessHours { get; set; }
    }

    public class ReservationMemberSummaryDto
    {
        public int MemberId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Membership { get; set; } = string.Empty;
        public string HomeClub { get; set; } = string.Empty;
        public int RemainingSessions { get; set; }
        public string ExpireAt { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public class ReservationSessionTypeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class ReservationReviewDto
    {
        public int Id { get; set; }
        public string Author { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ReservationDateSlotDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string SubLabel { get; set; } = string.Empty;
        public List<string> Times { get; set; } = new List<string>();
        public string? MoreLabel { get; set; }
    }

    public class ReservationTrainerCardDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public int ReviewCount { get; set; }
        public int Years { get; set; }
        public int ServedClients { get; set; }
        public int Satisfaction { get; set; }
        public decimal Price { get; set; }
        public string Club { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Highlight { get; set; } = string.Empty;
        public string HeroTone { get; set; } = string.Empty;
        public string AccentTone { get; set; } = string.Empty;
        public List<string> Goals { get; set; } = new List<string>();
        public List<string> Specialties { get; set; } = new List<string>();
        public List<string> Badges { get; set; } = new List<string>();
        public List<string> NextSlots { get; set; } = new List<string>();
    }

    public class ReservationTrainerDetailDto : ReservationTrainerCardDto
    {
        public string Introduction { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public List<string> Certifications { get; set; } = new List<string>();
        public List<ReservationSessionTypeDto> SessionTypes { get; set; } = new List<ReservationSessionTypeDto>();
        public List<ReservationDateSlotDto> AvailableDates { get; set; } = new List<ReservationDateSlotDto>();
        public List<ReservationReviewDto> Reviews { get; set; } = new List<ReservationReviewDto>();
    }

    public class ReservationBookingPageDto
    {
        public ReservationTrainerDetailDto Trainer { get; set; } = new ReservationTrainerDetailDto();
        public int RemainingSessions { get; set; }
    }

    public class ReservationOrderItemDto
    {
        public int Id { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int TrainerId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public string TrainerPhotoUrl { get; set; } = string.Empty;
        public int SessionTypeId { get; set; }
        public string SessionLabel { get; set; } = string.Empty;
        public string DateLabel { get; set; } = string.Empty;
        public string CalendarDate { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public string Club { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class ReservationProfileDto
    {
        public ReservationMemberSummaryDto User { get; set; } = new ReservationMemberSummaryDto();
        public int UpcomingCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
    }

    public class ReservationHomeDto
    {
        public ReservationMemberSummaryDto User { get; set; } = new ReservationMemberSummaryDto();
        public ReservationOrderItemDto? NextReservation { get; set; }
        public List<ReservationTrainerCardDto> RecommendedTrainers { get; set; } = new List<ReservationTrainerCardDto>();
    }

    public class ReservationCreateRequest
    {
        public int MemberId { get; set; }
        public int TrainerId { get; set; }
        public int SessionTypeId { get; set; }
        public string ReservationDate { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string? Remark { get; set; }
    }

    public class ReservationCreateResultDto
    {
        public int ReservationId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int RemainingSessions { get; set; }
    }

    public class ReservationCancelRequest
    {
        public int MemberId { get; set; }
        public int ReservationId { get; set; }
    }
}
