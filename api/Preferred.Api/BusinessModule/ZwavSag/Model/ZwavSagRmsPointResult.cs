using System;
using System.Collections.Generic;
namespace Zwav.Application.Sag
{
    public class ZwavSagRmsPointResult
    {
        public int ChannelIndex { get; set; }
        public string Phase { get; set; }
        public int SampleNo { get; set; }
        public double TimeMs { get; set; }
        public decimal Rms { get; set; }
        public decimal RmsPct { get; set; }
        public decimal ReferenceVoltage { get; set; }
        public int SeqNo { get; set; }
    }
}