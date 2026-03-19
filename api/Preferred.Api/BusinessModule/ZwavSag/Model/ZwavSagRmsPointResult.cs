namespace Zwav.Application.Sag
{
    /// <summary>
    /// RMS 点计算结果（纯内存）
    /// </summary>
    public class ZwavSagRmsPointResult
    {
        /// <summary>通道索引</summary>
        public int ChannelIndex { get; set; }

        /// <summary>相别（A/B/C 等）</summary>
        public string Phase { get; set; }

        /// <summary>采样点序号</summary>
        public int SampleNo { get; set; }

        /// <summary>时间（毫秒，相对波形起点）</summary>
        public double TimeMs { get; set; }

        /// <summary>RMS 值（单位同通道单位）</summary>
        public decimal Rms { get; set; }

        /// <summary>RMS 占参考电压百分比（%）</summary>
        public decimal RmsPct { get; set; }

        /// <summary>参考电压（单位同通道单位）</summary>
        public decimal ReferenceVoltage { get; set; }

        /// <summary>序号（用于排序）</summary>
        public int SeqNo { get; set; }
    }
}
