using System;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>暂降结果列表项（用于列表分页展示）</summary>
    public class ZwavSagListItemDto
    {
        /// <summary>事件ID</summary>
        public int Id { get; set; }

        /// <summary>录波文件ID</summary>
        public int FileId { get; set; }

        /// <summary>录波文件名</summary>
        public string OriginalName { get; set; }

        /// <summary>分析状态：0=待处理，1=处理中，2=成功，3=失败</summary>
        public int Status { get; set; }

        /// <summary>是否检测到暂降/中断事件</summary>
        public bool HasSag { get; set; }

        /// <summary>结果类型：Normal/Sag/Interruption</summary>
        public string EventType { get; set; }

        /// <summary>事件数量</summary>
        public int EventCount { get; set; }

        /// <summary>分析开始时间（UTC）</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>分析结束时间（UTC）</summary>
        public DateTime? FinishTime { get; set; }

        /// <summary>分析耗时（毫秒）</summary>
        public long? CostMs { get; set; }

        /// <summary>触发相</summary>
        public string TriggerPhase { get; set; }

        /// <summary>终止相</summary>
        public string EndPhase { get; set; }

        /// <summary>最严重相</summary>
        public string WorstPhase { get; set; }
        
        /// <summary>残余电压占比（%）</summary>
        public decimal? ResidualVoltagePct { get; set; }

        /// <summary>创建时间（UTC）</summary>
        public DateTime CrtTime { get; set; }
    }

    /// <summary>暂降结果详情（用于详情弹窗）</summary>
    public class ZwavSagDetailDto
    {
        /// <summary>事件ID</summary>
        public int Id { get; set; }

        /// <summary>录波文件ID</summary>
        public int FileId { get; set; }

        /// <summary>录波文件名</summary>
        public string OriginalName { get; set; }

        /// <summary>分析状态：0=待处理，1=处理中，2=成功，3=失败</summary>
        public int Status { get; set; }

        /// <summary>失败原因/错误信息（失败时填写）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>是否检测到暂降/中断事件</summary>
        public bool HasSag { get; set; }

        /// <summary>结果类型：Normal/Sag/Interruption</summary>
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

        /// <summary>事件持续时间（毫秒）</summary>
        public decimal? DurationMs { get; set; }

        /// <summary>触发相</summary>
        public string TriggerPhase { get; set; }

        /// <summary>终止相</summary>
        public string EndPhase { get; set; }

        /// <summary>最严重相</summary>
        public string WorstPhase { get; set; }

        /// <summary>参考电压类型（Declared/Sliding）</summary>
        public string ReferenceType { get; set; }

        /// <summary>参考电压（单位同通道单位）</summary>
        public decimal? ReferenceVoltage { get; set; }

        /// <summary>残余电压（单位同通道单位）</summary>
        public decimal? ResidualVoltage { get; set; }

        /// <summary>残余电压占比（%）</summary>
        public decimal? ResidualVoltagePct { get; set; }

        /// <summary>暂降深度（参考电压-残余电压）</summary>
        public decimal? SagDepth { get; set; }

        /// <summary>暂降幅值（%）</summary>
        public decimal? SagPercent { get; set; }

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

        /// <summary>备注</summary>
        public string Remark { get; set; }

        /// <summary>创建时间（UTC）</summary>
        public DateTime CrtTime { get; set; }
    }

    /// <summary>暂降事件相别明细（用于详情展示）</summary>
    public class ZwavSagPhaseDto
    {
        /// <summary>相别（A/B/C 等）</summary>
        public string Phase { get; set; }

        /// <summary>开始时间（UTC）</summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>结束时间（UTC）</summary>
        public DateTime EndTimeUtc { get; set; }

        /// <summary>持续时间（毫秒）</summary>
        public decimal DurationMs { get; set; }

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

        /// <summary>是否为触发相</summary>
        public bool IsTriggerPhase { get; set; }

        /// <summary>是否为结束相</summary>
        public bool IsEndPhase { get; set; }

        /// <summary>是否为最严重相</summary>
        public bool IsWorstPhase { get; set; }
    }

    /// <summary>更新暂降结果请求（当前仅支持少量字段，如备注）</summary>
    public class UpdateZwavSagEventRequest
    {
        /// <summary>备注</summary>
        public string Remark { get; set; }
    }

    /// <summary>发起暂降分析请求</summary>
    public class AnalyzeZwavSagRequest
    {
        /// <summary>录波文件ID集合（推荐）</summary>
        public int[] FileIds { get; set; }

        /// <summary>解析任务 GUID 集合（兼容输入，会反查 FileId）</summary>
        public string[] AnalysisGuids { get; set; }
        
        /// <summary>是否强制重建（会先删除该文件旧结果再生成新结果）</summary>
        public bool ForceRebuild { get; set; }

        /// <summary>参考电压类型（Declared/Sliding）</summary>
        public string ReferenceType { get; set; }

        /// <summary>参考电压（可选）</summary>
        public decimal? ReferenceVoltage { get; set; }

        /// <summary>暂降阈值（%）</summary>
        public decimal SagThresholdPct { get; set; } = 90m;

        /// <summary>中断阈值（%）</summary>
        public decimal InterruptThresholdPct { get; set; } = 10m;

        /// <summary>迟滞（%）</summary>
        public decimal HysteresisPct { get; set; } = 2m;

        /// <summary>最小持续时间（毫秒）</summary>
        public decimal MinDurationMs { get; set; } = 10m;
    }

    /// <summary>暂降分析响应</summary>
    public class AnalyzeZwavSagResponse
    {
        /// <summary>参与分析的文件数量</summary>
        public int AnalyzedCount { get; set; }

        /// <summary>创建的结果记录数量</summary>
        public int CreatedEventCount { get; set; }

        /// <summary>创建的相别明细数量</summary>
        public int CreatedPhaseCount { get; set; }

        /// <summary>创建的 RMS 点数量</summary>
        public int CreatedRmsPointCount { get; set; }
    }

    /// <summary>
    /// 暂降通道词库返回 DTO
    /// </summary>
    public class ZwavSagChannelRuleDto
    {
        /// <summary>主键</summary>
        public int Id { get; set; }

        /// <summary>规则名称/关键词</summary>
        public string RuleName { get; set; }

        /// <summary>排序号</summary>
        public int SeqNo { get; set; }

        /// <summary>创建时间（UTC）</summary>
        public DateTime CrtTime { get; set; }

        /// <summary>更新时间（UTC）</summary>
        public DateTime UpdTime { get; set; }
    }

    /// <summary>
    /// 新增暂降通道词库请求
    /// </summary>
    public class CreateZwavSagChannelRuleRequest
    {
        /// <summary>规则名称/关键词</summary>
        public string RuleName { get; set; }

        /// <summary>排序号</summary>
        public int SeqNo { get; set; }
    }

    /// <summary>
    /// 修改暂降通道词库请求
    /// </summary>
    public class UpdateZwavSagChannelRuleRequest
    {
        /// <summary>规则名称/关键词</summary>
        public string RuleName { get; set; }

        /// <summary>排序号</summary>
        public int? SeqNo { get; set; }
    }
}
