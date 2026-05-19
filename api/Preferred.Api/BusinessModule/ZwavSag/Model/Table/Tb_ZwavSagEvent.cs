using System;
using System.Collections.Generic;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 暂降分析结果（按录波文件维度）。
    /// 一条记录对应一个录波文件的一次分析结果；若未检测到暂降/中断，也会生成记录。
    /// </summary>
    public class ZwavSagEvent
    {
        /// <summary>主键</summary>
        public int Id { get; set; }

        /// <summary>录波文件ID（关联 Tb_ZwavFile）</summary>
        public int FileId { get; set; }

        /// <summary>录波文件原始名称</summary>
        public string OriginalName { get; set; }

        /// <summary>分析状态：0=待处理，1=处理中，2=成功，3=失败</summary>
        public int Status { get; set; }

        /// <summary>解析进度（0-100）</summary>
        public int Progress { get; set; }

        /// <summary>失败原因/错误信息</summary>
        public string ErrorMessage { get; set; }

        /// <summary>是否检测到暂降/中断事件</summary>
        public bool HasSag { get; set; }

        /// <summary>结果类型：Normal=正常，Sag=暂降，Interruption=中断</summary>
        public string EventType { get; set; }

        /// <summary>事件数量</summary>
        public int EventCount { get; set; }

        /// <summary>分析开始时间（UTC）</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>分析结束时间（UTC）</summary>
        public DateTime? FinishTime { get; set; }

        /// <summary>分析耗时（毫秒）</summary>
        public long? CostMs { get; set; }

        /// <summary>事件开始时间（UTC）</summary>
        public DateTime? StartTimeUtc { get; set; }

        /// <summary>事件结束时间（UTC）</summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>事件发生时间（UTC）</summary>
        public DateTime? OccurTimeUtc { get; set; }

        /// <summary>事件持续时长（毫秒）</summary>
        public decimal? DurationMs { get; set; }

        /// <summary>触发相别</summary>
        public string TriggerPhase { get; set; }

        /// <summary>恢复/结束相别</summary>
        public string EndPhase { get; set; }

        /// <summary>最严重相别</summary>
        public string WorstPhase { get; set; }

        /// <summary>参考电压类型（Declared/Sliding）</summary>
        public string ReferenceType { get; set; }

        /// <summary>参考电压</summary>
        public decimal? ReferenceVoltage { get; set; }

        /// <summary>残余电压</summary>
        public decimal? ResidualVoltage { get; set; }

        /// <summary>残余电压占比（%）</summary>
        public decimal? ResidualVoltagePct { get; set; }

        /// <summary>暂降深度</summary>
        public decimal? SagDepth { get; set; }

        /// <summary>暂降百分比（%）</summary>
        public decimal? SagPercent { get; set; }

        /// <summary>相角跳变</summary>
        public decimal? PhaseJumpDeg { get; set; }

        /// <summary>起始相角</summary>
        public decimal? StartAngleDeg { get; set; }

        /// <summary>暂降阈值（%）</summary>
        public decimal? SagThresholdPct { get; set; }

        /// <summary>中断阈值（%）</summary>
        public decimal? InterruptThresholdPct { get; set; }

        /// <summary>迟滞（%）</summary>
        public decimal? HysteresisPct { get; set; }

        /// <summary>是否为合并统计事件</summary>
        public bool IsMergedStatEvent { get; set; }

        /// <summary>合并分组ID</summary>
        public string MergeGroupId { get; set; }

        /// <summary>原始事件数量</summary>
        public int RawEventCount { get; set; }

        /// <summary>序号</summary>
        public int SeqNo { get; set; }

        /// <summary>备注</summary>
        public string Remark { get; set; }

        /// <summary>创建时间（UTC）</summary>
        public DateTime CrtTime { get; set; }

        /// <summary>更新时间（UTC）</summary>
        public DateTime UpdTime { get; set; }

        /// <summary>相别明细集合</summary>
        public virtual ICollection<ZwavSagEventPhase> Phases { get; set; }
    }
}
