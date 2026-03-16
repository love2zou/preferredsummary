using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
namespace Zwav.Application.Sag
{
    internal class RmsPoint
    {
        public int SampleNo { get; set; }
        public double TimeMs { get; set; }
        public decimal Rms { get; set; }
        public decimal RmsPct { get; set; }
    }

     internal class PhaseEventWindow
    {
        public string Phase { get; set; }

        public int? StartIndex { get; set; }
        public int? EndIndex { get; set; }

        public decimal ReferenceVoltage { get; set; }
        public decimal MinRms { get; set; } = decimal.MaxValue;

        public bool Triggered { get; set; }
        public bool Ended { get; set; }
    }
    internal class ChannelRmsSeries
    {
        public int ChannelIndex { get; set; }
        public string Phase { get; set; }
        public decimal ReferenceVoltage { get; set; }
        public List<RmsPoint> Points { get; set; } = new();
    }
}