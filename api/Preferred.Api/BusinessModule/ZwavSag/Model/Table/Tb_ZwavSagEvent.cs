using System;
using System.Collections.Generic;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 暂降分析结果主表。
    /// 一条记录对应一个录波文件的一次暂降分析结果。
    /// </summary>
    public class ZwavSagEvent
    {
        public int Id { get; set; }

        public int FileId { get; set; }

        public int? TaskId { get; set; }

        public int? AnalysisId { get; set; }

        public string AnalysisGuid { get; set; }

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

        public int SeqNo { get; set; }

        public DateTime CrtTime { get; set; }

        public DateTime UpdTime { get; set; }

        public virtual ICollection<ZwavSagEventPhase> Phases { get; set; }
    }
}
