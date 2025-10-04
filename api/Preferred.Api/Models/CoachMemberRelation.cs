using System;

namespace Preferred.Api.Models
{
    public class CoachMemberRelation
    {
        public int Id { get; set; }
        public int CoachId { get; set; }
        public int MemberId { get; set; }
        public int SeqNo { get; set; } = 0;
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}