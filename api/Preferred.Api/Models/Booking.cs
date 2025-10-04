using System;
using System.Collections.Generic;

namespace Preferred.Api.Models
{
    public class BoundCoachDto
    {
        public int CoachId { get; set; }
        public string CoachName { get; set; } = string.Empty;
    }

    public class AvailableSlotDto
    {
        public int SlotId { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class CreateBatchRequest
    {
        public int MemberId { get; set; }
        public int CoachId { get; set; }
        public string BookDate { get; set; } = string.Empty; // YYYY-MM-DD
        public List<TimeSlotItem> TimeSlots { get; set; } = new List<TimeSlotItem>();
    }

    public class TimeSlotItem
    {
        public string StartTime { get; set; } = string.Empty; // HH:mm
        public string EndTime { get; set; } = string.Empty;   // HH:mm
    }
    public class BoundMemberDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }
    public class BookingItemDto
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int CoachId { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string BookDate { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int Status { get; set; } = 0; // 0: 已预约；取消为删除记录
    }

    public class CancelRequest
    {
        public int Id { get; set; }
    }
}