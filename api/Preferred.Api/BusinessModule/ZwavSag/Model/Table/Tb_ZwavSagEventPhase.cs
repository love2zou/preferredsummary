using System;

namespace Preferred.Api.Models
{
    public class ZwavSagEventPhase
    {
        public int Id { get; set; }
        public int SagEventId { get; set; }
        public int AnalysisId { get; set; }

        public string Phase { get; set; }

        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public decimal DurationMs { get; set; }

        public string ReferenceType { get; set; }
        public decimal ReferenceVoltage { get; set; }

        public decimal ResidualVoltage { get; set; }
        public decimal ResidualVoltagePct { get; set; }

        public decimal SagDepth { get; set; }
        public decimal SagPercent { get; set; }

        public decimal? SagThresholdPct { get; set; }
        public decimal? InterruptThresholdPct { get; set; }
        public decimal? HysteresisPct { get; set; }

        public decimal? StartAngleDeg { get; set; }
        public decimal? PhaseJumpDeg { get; set; }

        public bool IsTriggerPhase { get; set; }
        public bool IsEndPhase { get; set; }
        public bool IsWorstPhase { get; set; }

        public int SeqNo { get; set; }
        public string Remark { get; set; }

        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        public virtual ZwavSagEvent SagEvent { get; set; }
        public virtual ZwavAnalysis Analysis { get; set; }
    }
}