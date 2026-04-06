using System;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 暂降分析上下文
    /// </summary>
    public class ZwavSagAnalyzeContext
    {
        public int AnalysisId { get; set; }

        public decimal FrequencyHz { get; set; }

        public decimal TimeMul { get; set; }

        public DateTime WaveStartTimeUtc { get; set; }

        public DateTime? TriggerTimeUtc { get; set; }

        /// <summary>参考电压类型（Declared / Sliding）</summary>
        public string ReferenceType { get; set; }

        /// <summary>强制参考电压（可空）</summary>
        public decimal? ReferenceVoltage { get; set; }

        /// <summary>暂降阈值（%）</summary>
        public decimal SagThresholdPct { get; set; }

        /// <summary>中断阈值（%）</summary>
        public decimal InterruptThresholdPct { get; set; }

        /// <summary>迟滞阈值（%）</summary>
        public decimal HysteresisPct { get; set; }

        /// <summary>恢复阈值（%）</summary>
        public decimal? RecoverThresholdPct { get; set; }

        /// <summary>最小时长（ms）</summary>
        public decimal MinDurationMs { get; set; }

        /// <summary>RMS 模式描述</summary>
        public string RmsMode { get; set; }

        /// <summary>电压通道</summary>
        public ZwavVoltageChannelContext[] VoltageChannels { get; set; } = Array.Empty<ZwavVoltageChannelContext>();

        /// <summary>
        /// 保留样本点结构，用于兼容旧逻辑/日志/时间轴
        /// </summary>
        public List<ZwavSagSamplePoint> Samples { get; set; } = new List<ZwavSagSamplePoint>();

        /// <summary>
        /// 时间轴（与 Samples 等长）
        /// </summary>
        public double[] TimeAxisMs { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 通道列缓存：ChannelIndex -> 全部采样值
        /// </summary>
        public Dictionary<int, double?[]> ChannelSeriesMap { get; set; }
            = new Dictionary<int, double?[]>();

        public Preferred.Api.Services.ZwavSagVoltageChannelRuleMatcher.RuleItem[] PhaseRules { get; set; }
            = Array.Empty<Preferred.Api.Services.ZwavSagVoltageChannelRuleMatcher.RuleItem>();

        /// <summary>
        /// 预计算的可分析采样段
        /// </summary>
        public List<ZwavSampleSegmentInfo> AnalyzableSegments { get; set; }
            = new List<ZwavSampleSegmentInfo>();
    }

    /// <summary>电压通道上下文</summary>
    public class ZwavVoltageChannelContext
    {
        /// <summary>通道索引</summary>
        public int ChannelIndex { get; set; }

        public string GroupName { get; set; }

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
        public Dictionary<int, double?> ChannelValues { get; set; } = new Dictionary<int, double?>();
    }
}
