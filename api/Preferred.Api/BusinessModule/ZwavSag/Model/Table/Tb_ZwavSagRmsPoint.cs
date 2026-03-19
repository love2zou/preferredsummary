using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// RMS 结果点（用于可视化/诊断）
    /// 由固定算法（1周波窗口 + 半周波更新）生成的 RMS 序列点。
    /// </summary>
    public class ZwavSagRmsPoint
    {
        /// <summary>主键</summary>
        public int Id { get; set; }

        public int SagEventId { get; set; }

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

        /// <summary>创建时间（UTC）</summary>
        public DateTime CrtTime { get; set; }

        /// <summary>更新时间（UTC）</summary>
        public DateTime UpdTime { get; set; }

        public virtual ZwavSagEvent SagEvent { get; set; }
    }
}
