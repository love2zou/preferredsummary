using System;
using Zwav.Application.Sag;

namespace Zwav.Application.Sag
{
    public class ZwavSagProcessDto
    {
        public int AnalysisId { get; set; }
        public string AnalysisGuid { get; set; }
        public DateTime WaveStartTimeUtc { get; set; }
        public decimal FrequencyHz { get; set; }
        public decimal TimeMul { get; set; }

        public ZwavSagVoltageChannelDto[] VoltageChannels { get; set; }
        public ZwavSagRmsPointDto[] RmsPoints { get; set; }
        public ZwavSagMarkerDto[] Markers { get; set; }
        public ZwavSagComputedEventDto[] ComputedEvents { get; set; }
        public ZwavSagProcessEventDto Event { get; set; }
        public ZwavSagProcessPhaseDto[] Phases { get; set; }

        public int? SuggestedFromSample { get; set; }
        public int? SuggestedToSample { get; set; }
    }

    public class ZwavSagProcessPreviewResponse
    {
        public ZwavSagRmsPointDto[] RmsPoints { get; set; }
        public ZwavSagMarkerDto[] Markers { get; set; }
        public ZwavSagComputedEventDto[] ComputedEvents { get; set; }
        public int? SuggestedFromSample { get; set; }
        public int? SuggestedToSample { get; set; }
    }

    public class ZwavSagVoltageChannelDto
    {
        public int ChannelIndex { get; set; }
        public string Phase { get; set; }
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string Unit { get; set; }
    }

    public class ZwavSagRmsPointDto
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

    public class ZwavSagMarkerDto
    {
        public string Kind { get; set; }
        public string Phase { get; set; }
        public double TimeMs { get; set; }
        public string Label { get; set; }
        public decimal? Value { get; set; }
    }

    public class ZwavSagComputedEventDto
    {
        public string EventType { get; set; }
        public int? ChannelIndex { get; set; }
        public string GroupName { get; set; }
        public string ChannelName { get; set; }
        public string Phase { get; set; }
        public DateTime OccurTimeUtc { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public double StartMs { get; set; }
        public double EndMs { get; set; }
        public decimal DurationMs { get; set; }
        public decimal ResidualVoltage { get; set; }
        public decimal ResidualVoltagePct { get; set; }
        public decimal SagMagnitudePct { get; set; }
    }

    public class ZwavSagProcessPreviewRequest
    {
        public string ReferenceType { get; set; }
        public decimal? ReferenceVoltage { get; set; }
        public decimal? RecoverThresholdPct { get; set; }
        public decimal? SagThresholdPct { get; set; }
        public decimal? InterruptThresholdPct { get; set; }
        public decimal? HysteresisPct { get; set; }
        public decimal? MinDurationMs { get; set; }
    }

    public class ZwavSagProcessEventDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string OriginalName { get; set; }
        public int Status { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasSag { get; set; }
        public string EventType { get; set; }
        public int EventCount { get; set; }
        public long? CostMs { get; set; }
        public string WorstPhase { get; set; }
        public string TriggerPhase { get; set; }
        public string EndPhase { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? OccurTimeUtc { get; set; }
        public decimal? DurationMs { get; set; }
        public decimal? ReferenceVoltage { get; set; }
        public decimal? ResidualVoltage { get; set; }
        public decimal? ResidualVoltagePct { get; set; }
        public decimal? SagPercent { get; set; }
        public decimal? SagThresholdPct { get; set; }
        public decimal? InterruptThresholdPct { get; set; }
        public decimal? HysteresisPct { get; set; }
        public decimal? MinDurationMs { get; set; }
    }

    public class ZwavSagProcessPhaseDto
    {
        public int? ChannelIndex { get; set; }
        public string GroupName { get; set; }
        public string ChannelName { get; set; }
        public string Phase { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public decimal DurationMs { get; set; }
        public decimal ReferenceVoltage { get; set; }
        public decimal ResidualVoltage { get; set; }
        public decimal ResidualVoltagePct { get; set; }
        public decimal SagDepth { get; set; }
        public decimal SagPercent { get; set; }
        public bool IsTriggerPhase { get; set; }
        public bool IsEndPhase { get; set; }
        public bool IsWorstPhase { get; set; }
    }
}
