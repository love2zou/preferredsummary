using System;

namespace Preferred.Api.Models
{
    public class ZwavChannel
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }

        public int ChannelIndex { get; set; }
        public string ChannelType { get; set; }  // Analog/Digital/Virtual
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string Phase { get; set; }
        public string Unit { get; set; }

        public decimal? RatioA { get; set; }
        public decimal? OffsetB { get; set; }
        public decimal? Skew { get; set; }

        public int IsEnable { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}
