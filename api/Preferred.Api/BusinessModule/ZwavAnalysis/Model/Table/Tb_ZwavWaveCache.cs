using System;

namespace Preferred.Api.Models
{
    public class ZwavWaveCache
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }

        public string CacheKey { get; set; }
        public int StartIndex { get; set; }  // 你表里 INT；建议后面改 BIGINT
        public int EndIndex { get; set; }

        public string Channels { get; set; }
        public int MaxPoints { get; set; }
        public string SampleMode { get; set; }
        public string PayloadJson { get; set; }

        public DateTime? ExpireTime { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}
