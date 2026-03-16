using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    public class ZwavSagAnalyzeContext
    {
        public int AnalysisId { get; set; }

        public decimal FrequencyHz { get; set; }
        public decimal TimeMul { get; set; }

        public DateTime WaveStartTimeUtc { get; set; }
        public DateTime? TriggerTimeUtc { get; set; }

        public string ReferenceType { get; set; }
        public decimal? ReferenceVoltage { get; set; }

        public decimal SagThresholdPct { get; set; }
        public decimal InterruptThresholdPct { get; set; }
        public decimal HysteresisPct { get; set; }
        public decimal MinDurationMs { get; set; }

        public string RmsMode { get; set; }

        public ZwavVoltageChannelContext[] VoltageChannels { get; set; }
        public IReadOnlyList<ZwavSagSamplePoint> Samples { get; set; }
    }

    public class ZwavVoltageChannelContext
    {
        public int ChannelIndex { get; set; }
        public string Phase { get; set; }
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string Unit { get; set; }
    }

    public class ZwavSagSamplePoint
    {
        public int SampleNo { get; set; }
        public long TimeRaw { get; set; }
        public double TimeMs { get; set; }

        /// <summary>
        /// key=ChannelIndex, value=模拟量值
        /// </summary>
        public Dictionary<int, double?> ChannelValues { get; set; }
    }
}