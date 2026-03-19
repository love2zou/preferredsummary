using System.Collections.Generic;
namespace Zwav.Application.Sag
{
    /// <summary>内部 RMS 点（用于算法计算）</summary>
    internal class RmsPoint
    {
        /// <summary>采样点序号</summary>
        public int SampleNo { get; set; }
        /// <summary>时间（毫秒）</summary>
        public double TimeMs { get; set; }
        /// <summary>RMS 值</summary>
        public decimal Rms { get; set; }
        /// <summary>RMS 占参考电压百分比（%）</summary>
        public decimal RmsPct { get; set; }
    }

     /// <summary>内部相别事件窗口（用于事件检测）</summary>
     internal class PhaseEventWindow
    {
        /// <summary>相别（A/B/C 等）</summary>
        public string Phase { get; set; }

        /// <summary>事件起始索引（对齐后的 RMS 序列索引）</summary>
        public int? StartIndex { get; set; }
        /// <summary>事件结束索引（对齐后的 RMS 序列索引）</summary>
        public int? EndIndex { get; set; }

        /// <summary>参考电压</summary>
        public decimal ReferenceVoltage { get; set; }
        /// <summary>窗口内最小 RMS（用于计算最严重相）</summary>
        public decimal MinRms { get; set; } = decimal.MaxValue;

        /// <summary>是否已触发</summary>
        public bool Triggered { get; set; }
        /// <summary>是否已恢复/结束</summary>
        public bool Ended { get; set; }
    }

    /// <summary>内部通道 RMS 序列</summary>
    internal class ChannelRmsSeries
    {
        /// <summary>通道索引</summary>
        public int ChannelIndex { get; set; }
        /// <summary>相别</summary>
        public string Phase { get; set; }
        /// <summary>参考电压</summary>
        public decimal ReferenceVoltage { get; set; }
        /// <summary>RMS 点序列</summary>
        public List<RmsPoint> Points { get; set; } = new();
    }
}
