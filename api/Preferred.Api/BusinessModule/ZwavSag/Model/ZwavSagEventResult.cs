using System;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    public class ZwavSagEventResult
    {
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

        public List<ZwavSagEventPhaseResult> Phases { get; set; } = new List<ZwavSagEventPhaseResult>();
    }
}