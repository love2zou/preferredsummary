using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    [Table("Tb_ReservationCoursePackage")]
    public class ReservationCoursePackage
    {
        public int Id { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        [Column(TypeName = "decimal(10,2)")]
        public decimal OriginalPrice { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal SalePrice { get; set; }
        public int TotalSessions { get; set; }
        public int ValidDays { get; set; }
        public string CoachLevel { get; set; } = string.Empty;
        public string ClubScope { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
        public string StatusCode { get; set; } = "Active";
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationCourseOrder")]
    public class ReservationCourseOrder
    {
        public int Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public int CoursePackageId { get; set; }
        public int Quantity { get; set; } = 1;
        [Column(TypeName = "decimal(10,2)")]
        public decimal OriginAmount { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal PointDiscountAmount { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal PayAmount { get; set; }
        public int? CouponId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string StatusCode { get; set; } = "PendingPayment";
        public DateTime OrderTime { get; set; }
        public DateTime? PayTime { get; set; }
        public DateTime? EffectiveTime { get; set; }
        public string Remark { get; set; } = string.Empty;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationCoupon")]
    public class ReservationCoupon
    {
        public int Id { get; set; }
        public string CouponCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CouponType { get; set; } = string.Empty;
        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountValue { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal MinAmount { get; set; }
        public string RuleText { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationMemberCoupon")]
    public class ReservationMemberCoupon
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int CouponId { get; set; }
        public int? CourseOrderId { get; set; }
        public string StatusCode { get; set; } = "Available";
        public DateTime ClaimedTime { get; set; }
        public DateTime? UsedTime { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationBodyMetric")]
    public class ReservationBodyMetric
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public DateTime MeasureTime { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal WeightKg { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal BodyFatRate { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal Bmi { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal MuscleKg { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationTrainingRecord")]
    public class ReservationTrainingRecord
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int CoachUserId { get; set; }
        public int? ReservationOrderId { get; set; }
        public DateTime RecordTime { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int Calories { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string StatusCode { get; set; } = "Completed";
        public string Summary { get; set; } = string.Empty;
        public string ActionNotesJson { get; set; } = string.Empty;
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationTrainingPlan")]
    public class ReservationTrainingPlan
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int? CoachUserId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ProgressText { get; set; } = string.Empty;
        public string StatusCode { get; set; } = "Active";
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationTrainingPlanItem")]
    public class ReservationTrainingPlanItem
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public string DayLabel { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int CaloriesTarget { get; set; }
        public bool IsCompleted { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationConversationMessage")]
    public class ReservationConversationMessage
    {
        public int Id { get; set; }
        public string SessionKey { get; set; } = string.Empty;
        public int MemberId { get; set; }
        public int CoachUserId { get; set; }
        public string SenderRole { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentTime { get; set; }
        public string StatusCode { get; set; } = "Sent";
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    [Table("Tb_ReservationCheckInRecord")]
    public class ReservationCheckInRecord
    {
        public int Id { get; set; }
        public int ReservationOrderId { get; set; }
        public int MemberId { get; set; }
        public int CoachUserId { get; set; }
        public string CheckInMethod { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public string ClubName { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public string StatusCode { get; set; } = "CheckedIn";
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ReservationFlowResponseDto
    {
        public ReservationOrderItemDto? UpcomingReservation { get; set; }
        public ReservationOrderItemDto? CompletedReservation { get; set; }
        public ReservationCheckInDto? CheckIn { get; set; }
        public List<ReservationConversationMessageDto> Messages { get; set; } = new List<ReservationConversationMessageDto>();
        public List<string> CancelReasons { get; set; } = new List<string>();
        public List<ReservationReviewMetricDto> ReviewMetrics { get; set; } = new List<ReservationReviewMetricDto>();
        public List<string> ReviewTags { get; set; } = new List<string>();
    }

    public class ReservationCheckInDto
    {
        public int ReservationOrderId { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string CheckInMethod { get; set; } = string.Empty;
        public string CheckInTime { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
    }

    public class ReservationConversationMessageDto
    {
        public string SenderRole { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SentTime { get; set; } = string.Empty;
    }

    public class ReservationReviewMetricDto
    {
        public string Label { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class ReservationCommerceResponseDto
    {
        public List<ReservationCoursePackageDto> Packages { get; set; } = new List<ReservationCoursePackageDto>();
        public ReservationCoursePackageDto? FeaturedPackage { get; set; }
        public List<ReservationCourseOrderDto> Orders { get; set; } = new List<ReservationCourseOrderDto>();
        public List<ReservationMemberCouponDto> Coupons { get; set; } = new List<ReservationMemberCouponDto>();
    }

    public class ReservationCoursePackageDto
    {
        public int Id { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal SalePrice { get; set; }
        public int TotalSessions { get; set; }
        public int ValidDays { get; set; }
        public string CoachLevel { get; set; } = string.Empty;
        public string ClubScope { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
        public string StatusCode { get; set; } = string.Empty;
    }

    public class ReservationCourseOrderDto
    {
        public int Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        public decimal OriginAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PointDiscountAmount { get; set; }
        public decimal PayAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string OrderTime { get; set; } = string.Empty;
    }

    public class ReservationMemberCouponDto
    {
        public int CouponId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CouponType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal MinAmount { get; set; }
        public string RuleText { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
    }

    public class ReservationMemberCenterResponseDto
    {
        public ReservationMemberProfileDto Profile { get; set; } = new ReservationMemberProfileDto();
        public List<ReservationBodyMetricDto> BodyMetrics { get; set; } = new List<ReservationBodyMetricDto>();
        public List<ReservationTrainingRecordDto> TrainingRecords { get; set; } = new List<ReservationTrainingRecordDto>();
        public ReservationTrainingPlanDto? TrainingPlan { get; set; }
        public List<ReservationMemberNotificationDto> Notifications { get; set; } = new List<ReservationMemberNotificationDto>();
    }

    public class ReservationMemberProfileDto
    {
        public int MemberId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal HeightCm { get; set; }
        public decimal WeightKg { get; set; }
        public string Birthday { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string MembershipName { get; set; } = string.Empty;
        public string HealthRemark { get; set; } = string.Empty;
        public string PrimaryGoal { get; set; } = string.Empty;
        public List<string> SecondaryGoals { get; set; } = new List<string>();
    }

    public class ReservationBodyMetricDto
    {
        public string MeasureTime { get; set; } = string.Empty;
        public decimal WeightKg { get; set; }
        public decimal BodyFatRate { get; set; }
        public decimal Bmi { get; set; }
        public decimal MuscleKg { get; set; }
    }

    public class ReservationTrainingRecordDto
    {
        public string RecordTime { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CoachName { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int Calories { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class ReservationTrainingPlanDto
    {
        public string PlanName { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string ProgressText { get; set; } = string.Empty;
        public List<ReservationTrainingPlanItemDto> Items { get; set; } = new List<ReservationTrainingPlanItemDto>();
    }

    public class ReservationTrainingPlanItemDto
    {
        public string DayLabel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int CaloriesTarget { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class ReservationMemberNotificationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string NotifyType { get; set; } = string.Empty;
        public string SendTime { get; set; } = string.Empty;
    }

    public class ReservationCoachWorkbenchResponseDto
    {
        public ReservationCoachDashboardDto Dashboard { get; set; } = new ReservationCoachDashboardDto();
        public ReservationCoachScheduleDto? Schedule { get; set; }
        public List<ReservationCoachReservationDto> PendingAudits { get; set; } = new List<ReservationCoachReservationDto>();
        public List<ReservationCoachMemberDto> Members { get; set; } = new List<ReservationCoachMemberDto>();
        public List<ReservationTrainingRecordDto> Records { get; set; } = new List<ReservationTrainingRecordDto>();
        public ReservationTrainingPlanDto? TrainingPlan { get; set; }
        public List<ReservationConversationMessageDto> Messages { get; set; } = new List<ReservationConversationMessageDto>();
    }
}
