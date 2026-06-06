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

    public class AnalyzeZwavSagRequest
    {
        public int? TaskId { get; set; }
        public bool CreateTask { get; set; }
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
        public int TaskId { get; set; }
        public string TaskNo { get; set; }
        public int AnalyzedCount { get; set; }
        public int QueuedCount { get; set; }
        public int[] QueuedEventIds { get; set; }
        public int CreatedEventCount { get; set; }
        public int CreatedPhaseCount { get; set; }
        public int CreatedRmsPointCount { get; set; }
        public int AddedFileCount { get; set; }
        public int SkippedFileCount { get; set; }
    }

    public class ZwavSagTaskDto
    {
        public int Id { get; set; }
        public string TaskNo { get; set; }
        public string TaskName { get; set; }
        public string SourceType { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
        public int Progress { get; set; }
        public bool IsClosed { get; set; }
        public DateTime? ClosedTime { get; set; }
        public DateTime? StartParseTime { get; set; }
        public DateTime? FinishParseTime { get; set; }
        public DateTime? LastReceiveTime { get; set; }
        public string ReferenceType { get; set; }
        public decimal? ReferenceVoltage { get; set; }
        public decimal SagThresholdPct { get; set; }
        public decimal? RecoverThresholdPct { get; set; }
        public decimal InterruptThresholdPct { get; set; }
        public decimal HysteresisPct { get; set; }
        public decimal MinDurationMs { get; set; }
        public int ReceivedFileCount { get; set; }
        public int FinishedFileCount { get; set; }
        public int SuccessFileCount { get; set; }
        public int FailedFileCount { get; set; }
        public int PendingFileCount { get; set; }
        public long TotalParseMs { get; set; }
        public long? EstimatedRemainingMs { get; set; }
        public string SummaryText { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ZwavSagTaskFileItemDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public int? AnalysisId { get; set; }
        public string AnalysisGuid { get; set; }
        public string OriginalName { get; set; }
        public int Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasSag { get; set; }
        public string EventType { get; set; }
        public string WorstPhase { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public long? CostMs { get; set; }
        public DateTime? OccurTimeUtc { get; set; }
        public decimal? DurationMs { get; set; }
        public decimal? SagPercent { get; set; }
        public decimal? ResidualVoltagePct { get; set; }
        public DateTime CrtTime { get; set; }
    }

    public class UpdateZwavSagTaskRequest
    {
        public string TaskName { get; set; }
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
