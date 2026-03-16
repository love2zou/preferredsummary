using System;

namespace Preferred.Api.Models
{
    public class ZwavSagRmsPoint
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }
        public int ChannelIndex { get; set; }
        public string Phase { get; set; }
        public int SampleNo { get; set; }
        public double TimeMs { get; set; }
        public decimal Rms { get; set; }
        public decimal RmsPct { get; set; }
        public decimal ReferenceVoltage { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        public virtual ZwavAnalysis Analysis { get; set; }
    }
}