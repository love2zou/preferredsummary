using System;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 暂降分析上下文
    /// 包含采样点、通道信息、频率、阈值等，用于分析器进行纯计算。
    /// </summary>
    public class ZwavSagAnalyzeContext
    {
        /// <summary>解析任务ID（用于追溯波形来源）</summary>
        public int AnalysisId { get; set; }

        /// <summary>系统频率（Hz）</summary>
        public decimal FrequencyHz { get; set; }

        /// <summary>时间倍率（来自 CFG，TimeRaw * TimeMul -> ms 等换算用）</summary>
        public decimal TimeMul { get; set; }

        /// <summary>波形起始时间（UTC）</summary>
        public DateTime WaveStartTimeUtc { get; set; }

        /// <summary>触发时间（UTC，可选）</summary>
        public DateTime? TriggerTimeUtc { get; set; }

        /// <summary>参考电压类型（Declared=公称，Sliding=滑动参考）</summary>
        public string ReferenceType { get; set; }

        /// <summary>参考电压（可选；不传则由算法估算）</summary>
        public decimal? ReferenceVoltage { get; set; }

        /// <summary>暂降阈值（%）</summary>
        public decimal SagThresholdPct { get; set; }

        /// <summary>中断阈值（%）</summary>
        public decimal InterruptThresholdPct { get; set; }

        /// <summary>迟滞（%）</summary>
        public decimal HysteresisPct { get; set; }

        /// <summary>最小持续时间（毫秒）</summary>
        public decimal MinDurationMs { get; set; }

        /// <summary>RMS 模式（历史字段：当前后端固定为 1周波窗口 + 半周波更新）</summary>
        public string RmsMode { get; set; }

        /// <summary>电压通道集合（用于参与暂降/中断判定）</summary>
        public ZwavVoltageChannelContext[] VoltageChannels { get; set; }

        /// <summary>采样点序列（按时间排序）</summary>
        public IReadOnlyList<ZwavSagSamplePoint> Samples { get; set; }
    }

    /// <summary>电压通道上下文</summary>
    public class ZwavVoltageChannelContext
    {
        /// <summary>通道索引</summary>
        public int ChannelIndex { get; set; }

        /// <summary>相别（A/B/C 等）</summary>
        public string Phase { get; set; }

        /// <summary>通道编码</summary>
        public string ChannelCode { get; set; }

        /// <summary>通道名称</summary>
        public string ChannelName { get; set; }

        /// <summary>单位（如 V/kV 等）</summary>
        public string Unit { get; set; }
    }

    /// <summary>采样点（包含多通道模拟量）</summary>
    public class ZwavSagSamplePoint
    {
        /// <summary>采样点序号</summary>
        public int SampleNo { get; set; }

        /// <summary>原始时间戳（来自 DAT/HDR）</summary>
        public long TimeRaw { get; set; }

        /// <summary>时间（毫秒，相对波形起点）</summary>
        public double TimeMs { get; set; }

        /// <summary>
        /// 模拟量值字典：key=ChannelIndex，value=通道值
        /// </summary>
        public Dictionary<int, double?> ChannelValues { get; set; }
    }
}
