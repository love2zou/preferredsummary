using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Preferred.Api.Models
{
    public class ReservationLookupOptionDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class ReservationClubAdminSearchParams
    {
        public string? Keyword { get; set; }
    }

    public class ReservationClubAdminDto
    {
        public int Id { get; set; }
        public string ClubCode { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? BusinessHours { get; set; }
        public bool IsActive { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationClubAdminEditDto
    {
        [Required]
        [StringLength(50)]
        public string ClubCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ClubName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        [StringLength(50)]
        public string? District { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? BusinessHours { get; set; }

        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
    }

    public class ReservationTrainerAdminSearchParams
    {
        public string? Keyword { get; set; }
        public int? ClubId { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ReservationTrainerAdminDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int ClubId { get; set; }
        public string ClubName { get; set; } = string.Empty;
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
        public bool IsRecommended { get; set; }
        public bool IsActive { get; set; }
        public int SeqNo { get; set; }
        public List<string> Goals { get; set; } = new List<string>();
        public List<string> Specialties { get; set; } = new List<string>();
        public List<string> Badges { get; set; } = new List<string>();
        public List<string> Certifications { get; set; } = new List<string>();
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationTrainerAdminEditDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int ClubId { get; set; }

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Gender { get; set; } = string.Empty;

        public int YearsOfExperience { get; set; }
        public decimal Rating { get; set; }
        public int ReviewCount { get; set; }
        public int ServedClients { get; set; }
        public int Satisfaction { get; set; }
        public decimal BasePrice { get; set; }

        [StringLength(100)]
        public string? TrainingArea { get; set; }

        [StringLength(100)]
        public string? Highlight { get; set; }

        [StringLength(1000)]
        public string? Introduction { get; set; }

        [StringLength(1000)]
        public string? Story { get; set; }

        [StringLength(500)]
        public string? HeroImageUrl { get; set; }

        [StringLength(20)]
        public string? HeroTone { get; set; }

        [StringLength(20)]
        public string? AccentTone { get; set; }

        public bool IsRecommended { get; set; }
        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
        public List<string> Goals { get; set; } = new List<string>();
        public List<string> Specialties { get; set; } = new List<string>();
        public List<string> Badges { get; set; } = new List<string>();
        public List<string> Certifications { get; set; } = new List<string>();
    }

    public class ReservationSessionAdminSearchParams
    {
        public int? TrainerProfileId { get; set; }
        public string? Keyword { get; set; }
    }

    public class ReservationSessionAdminDto
    {
        public int Id { get; set; }
        public int TrainerProfileId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public string SessionCode { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationSessionAdminEditDto
    {
        [Required]
        public int TrainerProfileId { get; set; }

        [Required]
        [StringLength(50)]
        public string SessionCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string SessionName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int DurationMinutes { get; set; } = 60;
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
    }

    public class ReservationScheduleAdminSearchParams
    {
        public int? TrainerProfileId { get; set; }
        public int? ClubId { get; set; }
        public string? ScheduleDate { get; set; }
        public bool? IsAvailable { get; set; }
    }

    public class ReservationScheduleAdminDto
    {
        public int Id { get; set; }
        public int TrainerProfileId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public int ClubId { get; set; }
        public string ClubName { get; set; } = string.Empty;
        public string ScheduleDate { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationScheduleAdminEditDto
    {
        [Required]
        public int TrainerProfileId { get; set; }

        [Required]
        public int ClubId { get; set; }

        [Required]
        public string ScheduleDate { get; set; } = string.Empty;

        [Required]
        public string StartTime { get; set; } = string.Empty;

        [Required]
        public string EndTime { get; set; } = string.Empty;

        public bool IsAvailable { get; set; } = true;
        public int SeqNo { get; set; }
    }

    public class ReservationPackageAdminSearchParams
    {
        public int? MemberId { get; set; }
        public int? ClubId { get; set; }
        public string? StatusCode { get; set; }
    }

    public class ReservationPackageAdminDto
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public int ClubId { get; set; }
        public string ClubName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string? MembershipName { get; set; }
        public int TotalSessions { get; set; }
        public int RemainingSessions { get; set; }
        public string EffectiveDate { get; set; } = string.Empty;
        public string ExpireDate { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationPackageAdminEditDto
    {
        [Required]
        public int MemberId { get; set; }

        [Required]
        public int ClubId { get; set; }

        [Required]
        [StringLength(100)]
        public string PackageName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? MembershipName { get; set; }

        public int TotalSessions { get; set; }
        public int RemainingSessions { get; set; }

        [Required]
        public string EffectiveDate { get; set; } = string.Empty;

        [Required]
        public string ExpireDate { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string StatusCode { get; set; } = "Active";

        public int SeqNo { get; set; }
    }

    public class ReservationOrderAdminSearchParams
    {
        public int? MemberId { get; set; }
        public int? TrainerProfileId { get; set; }
        public string? StatusCode { get; set; }
    }

    public class ReservationOrderAdminDto
    {
        public int Id { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public int TrainerProfileId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public int ClubId { get; set; }
        public string ClubName { get; set; } = string.Empty;
        public int SessionTypeId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public string ReservationDate { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public decimal PriceAmount { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string? Remark { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationOrderStatusUpdateDto
    {
        [Required]
        [StringLength(20)]
        public string StatusCode { get; set; } = string.Empty;
    }
}
