using System;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 暂降事件相别计算结果（纯内存）
    /// </summary>
    public class ZwavSagEventPhaseResult
    {
        /// <summary>通道索引</summary>
        public int ChannelIndex { get; set; }
        public string GroupName { get; set; }
        /// <summary>通道名称</summary>
        public string ChannelName { get; set; }
        /// <summary>相别（A/B/C 等）</summary>
        public string Phase { get; set; }

        /// <summary>开始时间（UTC）</summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>结束时间（UTC）</summary>
        public DateTime EndTimeUtc { get; set; }

        /// <summary>持续时间（毫秒）</summary>
        public decimal DurationMs { get; set; }

        /// <summary>参考电压类型（Declared/Sliding）</summary>
        public string ReferenceType { get; set; }

        /// <summary>参考电压（单位同通道单位）</summary>
        public decimal ReferenceVoltage { get; set; }

        /// <summary>残余电压（单位同通道单位）</summary>
        public decimal ResidualVoltage { get; set; }

        /// <summary>残余电压占比（%）</summary>
        public decimal ResidualVoltagePct { get; set; }

        /// <summary>暂降深度（参考电压-残余电压）</summary>
        public decimal SagDepth { get; set; }

        /// <summary>暂降幅值（%）</summary>
        public decimal SagPercent { get; set; }

        /// <summary>暂降阈值（%）</summary>
        public decimal? SagThresholdPct { get; set; }

        /// <summary>中断阈值（%）</summary>
        public decimal? InterruptThresholdPct { get; set; }

        /// <summary>迟滞（%）</summary>
        public decimal? HysteresisPct { get; set; }

        /// <summary>起始相角（度，可选）</summary>
        public decimal? StartAngleDeg { get; set; }

        /// <summary>相角跳变（度，可选）</summary>
        public decimal? PhaseJumpDeg { get; set; }

        /// <summary>是否为触发相</summary>
        public bool IsTriggerPhase { get; set; }

        /// <summary>是否为结束相</summary>
        public bool IsEndPhase { get; set; }

        /// <summary>是否为最严重相</summary>
        public bool IsWorstPhase { get; set; }
    }
}
