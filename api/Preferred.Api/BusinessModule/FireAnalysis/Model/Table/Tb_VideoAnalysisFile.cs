using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 视频分析任务关联的视频文件表
    /// </summary>
    public class VideoAnalysisFile
    {
        public int Id { get; set; }

        /// <summary>
        /// 视频分析任务ID（Tb_VideoAnalysisJob.Id）
        /// </summary>
        public int JobId { get; set; }

        /// <summary>
        /// 视频原始文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 视频文件存储路径
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// 识别事件数
        /// </summary>
        public int? EventCount { get; set; }

        /// <summary>
        /// 分析耗时(秒)
        /// </summary>
        public int? AnalyzeMs { get; set; }

        /// <summary>
        /// 视频时长（秒）
        /// </summary>
        public int? DurationSec { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }

        /// <summary>
        /// 视频处理状态：0=待处理，1=处理中，2=完成，3=失败
        /// </summary>
        public byte Status { get; set; }

        public string ErrorMessage { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        // 可选导航（如你不需要可删）
        public VideoAnalysisJob Job { get; set; }
    }
}