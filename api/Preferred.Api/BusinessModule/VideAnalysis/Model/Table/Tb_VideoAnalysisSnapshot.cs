using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 视频闪光/火花事件截图表（已画框，人工复核用）
    /// </summary>
    public class VideoAnalysisSnapshot
    {
        public int Id { get; set; }
        /// <summary>
        /// 视频文件ID（Tb_VideoAnalysisFile.Id）
        /// </summary>
        public int VideoFileId { get; set; }
        /// <summary>
        /// 所属识别事件ID（Tb_VideoAnalysisEvent.Id）
        /// </summary>
        public int EventId { get; set; }

        /// <summary>
        /// 已画框截图文件路径
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// 截图对应视频时间点（秒）
        /// </summary>
        public int TimeSec { get; set; }

        /// <summary>
        /// 截图对应帧序号
        /// </summary>
        public int FrameIndex { get; set; }

        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        /// <summary>
        /// 该截图对应的置信度（0~1）
        /// </summary>
        public decimal Confidence { get; set; }  // ★新增
        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        // 可选导航
        public VideoAnalysisEvent Event { get; set; }
    }
}