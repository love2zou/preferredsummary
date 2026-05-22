using System;

namespace Zwav.Application.Sag
{
    public class ZwavSagListItemDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string OriginalName { get; set; }
        public int Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasSag { get; set; }
        public string EventType { get; set; }
        public string WorstPhase { get; set; }
        public decimal? ResidualVoltagePct { get; set; }
        public decimal? DurationMs { get; set; }
        public decimal? SagPercent { get; set; }
        public decimal? ResidualVoltage { get; set; }
        public DateTime? OccurTimeUtc { get; set; }
        public string OccurTimeText { get; set; }
        public DateTime CrtTime { get; set; }
    }

    public class ZwavSagDetailDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string OriginalName { get; set; }
        public int Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasSag { get; set; }
        public string EventType { get; set; }
        public int EventCount { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public long? CostMs { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? OccurTimeUtc { get; set; }
        public decimal? DurationMs { get; set; }
        public string TriggerPhase { get; set; }
        public string EndPhase { get; set; }
        public string WorstPhase { get; set; }
        public string ReferenceType { get; set; }
        public decimal? RecoverThresholdPct { get; set; }
        public decimal? ReferenceVoltage { get; set; }
        public decimal? ResidualVoltage { get; set; }
        public decimal? ResidualVoltagePct { get; set; }
        public decimal? SagDepth { get; set; }
        public decimal? SagPercent { get; set; }
        public decimal? PhaseJumpDeg { get; set; }
        public decimal? StartAngleDeg { get; set; }
        public decimal? SagThresholdPct { get; set; }
        public decimal? InterruptThresholdPct { get; set; }
        public decimal? HysteresisPct { get; set; }
        public bool IsMergedStatEvent { get; set; }
        public string MergeGroupId { get; set; }
        public int RawEventCount { get; set; }
        public string Remark { get; set; }
        public DateTime CrtTime { get; set; }
    }

    public class ZwavSagPhaseDto
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

    public class UpdateZwavSagEventRequest
    {
        public string Remark { get; set; }
    }

    public class AnalyzeZwavSagRequest
    {
        public int[] FileIds { get; set; }
        public string[] AnalysisGuids { get; set; }
        public string ReferenceType { get; set; } = "Declared";
        public decimal? ReferenceVoltage { get; set; }
        public decimal SagThresholdPct { get; set; } = 90m;
        public decimal? RecoverThresholdPct { get; set; }
        public decimal InterruptThresholdPct { get; set; } = 10m;
        public decimal HysteresisPct { get; set; } = 2m;
        public decimal MinDurationMs { get; set; } = 0m;
        public bool ForceRebuild { get; set; }
    }

    public class AnalyzeZwavSagResponse
    {
        public int AnalyzedCount { get; set; }
        public int QueuedCount { get; set; }
        public int[] QueuedEventIds { get; set; }
        public int CreatedEventCount { get; set; }
        public int CreatedPhaseCount { get; set; }
        public int CreatedRmsPointCount { get; set; }
    }

    public class ZwavSagChannelRuleDto
    {
        public int Id { get; set; }
        public string RuleName { get; set; }
        public string PhaseName { get; set; }
        public int PhaseType { get; set; }
        public decimal PhaseValue { get; set; }
        public int SeqNo { get; set; }
        public bool Enabled { get; set; }
        public DateTime CrtTime { get; set; }
    }

    public class CreateZwavSagChannelRuleRequest
    {
        public string RuleName { get; set; }
        public string PhaseName { get; set; }
        public int PhaseType { get; set; }
        public decimal PhaseValue { get; set; }
        public int SeqNo { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class UpdateZwavSagChannelRuleRequest
    {
        public string RuleName { get; set; }
        public string PhaseName { get; set; }
        public int? PhaseType { get; set; }
        public decimal? PhaseValue { get; set; }
        public int? SeqNo { get; set; }
        public bool? Enabled { get; set; }
    }

    public class ZwavSagGroupRuleDto
    {
        public int Id { get; set; }
        public string RuleName { get; set; }
        public string GroupName { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
    }

    public class CreateZwavSagGroupRuleRequest
    {
        public string RuleName { get; set; }
        public string GroupName { get; set; }
        public int SeqNo { get; set; }
    }

    public class UpdateZwavSagGroupRuleRequest
    {
        public string RuleName { get; set; }
        public string GroupName { get; set; }
        public int? SeqNo { get; set; }
    }
}
