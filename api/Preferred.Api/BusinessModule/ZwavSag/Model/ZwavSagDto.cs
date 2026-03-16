using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    public class ZwavSagListItemDto
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }
        public string OriginalName { get; set; }
        public string EventType { get; set; }
        public DateTime OccurTimeUtc { get; set; }
        public decimal SagPercent { get; set; }
        public decimal DurationMs { get; set; }

        public string TriggerPhase { get; set; }
        public string EndPhase { get; set; }
        public string WorstPhase { get; set; }
        
        public decimal ResidualVoltagePct { get; set; }
    }

    public class ZwavSagDetailDto
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }
        public string EventType { get; set; }

        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public DateTime OccurTimeUtc { get; set; }
        public decimal DurationMs { get; set; }

        public string TriggerPhase { get; set; }
        public string EndPhase { get; set; }
        public string WorstPhase { get; set; }

        public string ReferenceType { get; set; }
        public decimal ReferenceVoltage { get; set; }

        public decimal ResidualVoltage { get; set; }
        public decimal ResidualVoltagePct { get; set; }

        public decimal SagDepth { get; set; }
        public decimal SagPercent { get; set; }

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
        public string[] AnalysisGuids { get; set; }
        
        public bool ForceRebuild { get; set; }

        public string ReferenceType { get; set; }
        public decimal? ReferenceVoltage { get; set; }
        public decimal SagThresholdPct { get; set; } = 90m;
        public decimal InterruptThresholdPct { get; set; } = 10m;
        public decimal HysteresisPct { get; set; } = 2m;
        public decimal MinDurationMs { get; set; } = 10m;
        public string RmsMode { get; set; } = "HalfCycle";
    }

    public class AnalyzeZwavSagResponse
    {
        public int AnalyzedCount { get; set; }
        public int CreatedEventCount { get; set; }
        public int CreatedPhaseCount { get; set; }
        public int CreatedRmsPointCount { get; set; }
    }

    /// <summary>
    /// 暂降通道词库返回 DTO
    /// </summary>
    public class ZwavSagChannelRuleDto
    {
        public int Id { get; set; }
        public string RuleName { get; set; }
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 新增暂降通道词库请求
    /// </summary>
    public class CreateZwavSagChannelRuleRequest
    {
        public string RuleName { get; set; }
        public int SeqNo { get; set; }
    }

    /// <summary>
    /// 修改暂降通道词库请求
    /// </summary>
    public class UpdateZwavSagChannelRuleRequest
    {
        public string RuleName { get; set; }
        public int? SeqNo { get; set; }
    }
}

