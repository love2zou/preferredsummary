using System;

namespace Preferred.Api.Models
{
    public class BookTask
    {
        public int Id { get; set; }
        public int CoachId { get; set; }
        public int MemberId { get; set; }
        public DateTime BookDate { get; set; }
        public string BookTimeSlot { get; set; } = string.Empty; // 例如 09:00-09:30
        public int SeqNo { get; set; } = 0;
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}