using System;

namespace Preferred.Api.Models
{
    public class ZwavSagGroupRule
    {
        public int Id { get; set; }

        public string RuleName { get; set; }

        public string GroupName { get; set; }

        public int SeqNo { get; set; }

        public DateTime CrtTime { get; set; }

        public DateTime UpdTime { get; set; }
    }
}
