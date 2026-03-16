using System;
using System.Collections.Generic;

namespace Preferred.Api.Models
{
    public class ZwavSagChannelRule
    {
        public int Id { get; set; }

        public string RuleName { get; set; }

        public int SeqNo { get; set; }

        public DateTime CrtTime { get; set; }

        public DateTime UpdTime { get; set; }
    }
}