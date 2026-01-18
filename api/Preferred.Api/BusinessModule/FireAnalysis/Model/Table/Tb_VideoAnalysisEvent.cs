using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 视频闪光/火花识别事件结果表
    /// </summary>
    public class VideoAnalysisEvent
    {
        public int Id { get; set; }

        public int JobId { get; set; }

        /// <summary>
        /// 视频文件ID（Tb_VideoAnalysisFile.Id）
        /// </summary>
        public int VideoFileId { get; set; }

        /// <summary>
        /// 事件类型：1=闪光，2=火花
        /// </summary>
        public byte EventType { get; set; }

        /// <summary>
        /// 事件开始时间点（秒）
        /// </summary>
        public int StartTimeSec { get; set; }

        /// <summary>
        /// 事件结束时间点（秒）
        /// </summary>
        public int EndTimeSec { get; set; }

        /// <summary>
        /// 事件峰值时间点（秒）
        /// </summary>
        public int PeakTimeSec { get; set; }

        /// <summary>
        /// 峰值帧序号
        /// </summary>
        public int FrameIndex { get; set; }

        /// <summary>
        /// 识别置信度（0-1）
        /// </summary>
        public decimal Confidence { get; set; }

        /// <summary>
        /// 事件边界框信息(JSON：x,y,w,h)
        /// </summary>
        public string BBoxJson { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        // 可选导航
        public VideoAnalysisJob Job { get; set; }
        public VideoAnalysisFile VideoFile { get; set; }
    }
}