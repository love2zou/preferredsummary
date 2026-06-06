using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 暂降分析任务聚合表。
    /// 一个任务可以持续接收多批录波文件，直到被手动关闭。
    /// </summary>
    public class ZwavSagTask
    {
        public int Id { get; set; }

        public string TaskNo { get; set; }

        public string TaskName { get; set; }

        /// <summary>任务来源：Manual/TripEvent。</summary>
        public string SourceType { get; set; }

        /// <summary>任务状态：0=接收中，1=已关闭待收尾，2=完成，3=完成但存在失败。</summary>
        public int Status { get; set; }

        public int Progress { get; set; }

        public bool IsClosed { get; set; }

        public DateTime? ClosedTime { get; set; }

        public DateTime? StartParseTime { get; set; }

        public DateTime? FinishParseTime { get; set; }

        public DateTime? LastReceiveTime { get; set; }

        public string ReferenceType { get; set; }

        public decimal? ReferenceVoltage { get; set; }

        public decimal SagThresholdPct { get; set; }

        public decimal? RecoverThresholdPct { get; set; }

        public decimal InterruptThresholdPct { get; set; }

        public decimal HysteresisPct { get; set; }

        public decimal MinDurationMs { get; set; }

        public int ReceivedFileCount { get; set; }

        public int FinishedFileCount { get; set; }

        public int SuccessFileCount { get; set; }

        public int FailedFileCount { get; set; }

        public int PendingFileCount { get; set; }

        public long TotalParseMs { get; set; }

        public string ErrorMessage { get; set; }

        public int SeqNo { get; set; }

        public DateTime CrtTime { get; set; }

        public DateTime UpdTime { get; set; }
    }
}
