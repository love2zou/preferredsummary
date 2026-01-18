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

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        // 可选导航
        public VideoAnalysisEvent Event { get; set; }
    }
}