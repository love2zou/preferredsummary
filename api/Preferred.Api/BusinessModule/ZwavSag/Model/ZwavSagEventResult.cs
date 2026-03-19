using System;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 暂降事件计算结果（纯内存）
    /// 用于分析器输出，后续可落库到 Tb_ZwavSagEvent / Tb_ZwavSagEventPhase。
    /// </summary>
    public class ZwavSagEventResult
    {
        /// <summary>事件类型：Sag/Interruption</summary>
        public string EventType { get; set; }

        /// <summary>事件开始时间（UTC）</summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>事件结束时间（UTC）</summary>
        public DateTime EndTimeUtc { get; set; }

        /// <summary>事件发生时间（UTC，一般取开始时间）</summary>
        public DateTime OccurTimeUtc { get; set; }

        /// <summary>事件持续时间（毫秒）</summary>
        public decimal DurationMs { get; set; }

        /// <summary>触发相</summary>
        public string TriggerPhase { get; set; }

        /// <summary>终止相</summary>
        public string EndPhase { get; set; }

        /// <summary>最严重相</summary>
        public string WorstPhase { get; set; }

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

        /// <summary>相角跳变（度，可选）</summary>
        public decimal? PhaseJumpDeg { get; set; }

        /// <summary>起始相角（度，可选）</summary>
        public decimal? StartAngleDeg { get; set; }

        /// <summary>暂降阈值（%）</summary>
        public decimal? SagThresholdPct { get; set; }

        /// <summary>中断阈值（%）</summary>
        public decimal? InterruptThresholdPct { get; set; }

        /// <summary>迟滞（%）</summary>
        public decimal? HysteresisPct { get; set; }

        /// <summary>是否为合并统计事件（保留字段）</summary>
        public bool IsMergedStatEvent { get; set; }

        /// <summary>合并分组ID（保留字段）</summary>
        public string MergeGroupId { get; set; }

        /// <summary>原始事件数量（保留字段）</summary>
        public int RawEventCount { get; set; }

        /// <summary>相别明细集合</summary>
        public List<ZwavSagEventPhaseResult> Phases { get; set; } = new List<ZwavSagEventPhaseResult>();
    }
}
