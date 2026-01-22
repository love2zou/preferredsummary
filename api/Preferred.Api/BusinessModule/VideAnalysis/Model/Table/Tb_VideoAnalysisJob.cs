using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 视频智能分析任务表（多视频闪光/火花检测）
    /// </summary>
    public class VideoAnalysisJob
    {
        public int Id { get; set; }

        public string JobNo { get; set; }

        /// <summary>
        /// 任务状态：0=待处理，1=处理中，2=完成，3=失败，4=取消
        /// </summary>
        public byte Status { get; set; }

        /// <summary>
        /// 任务执行进度（0-100）
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// 算法标识（如 spark_v1）
        /// </summary>
        public string AlgoCode { get; set; }

        /// <summary>
        /// 算法参数配置(JSON)
        /// </summary>
        public string AlgoParamsJson { get; set; }

        public int TotalVideoCount { get; set; }
        public int FinishedVideoCount { get; set; }
        public int TotalEventCount { get; set; }

        public string ErrorMessage { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}