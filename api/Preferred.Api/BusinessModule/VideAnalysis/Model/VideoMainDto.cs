using System;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using Preferred.Api.Models;

namespace Video.Application.Dto
{
    public sealed class CreateVideoJobRequest
    {
        public string AlgoParamsJson { get; set; }
    }
     public sealed class UploadVideoResultDto
    {
        public string JobNo { get; set; }
        public int JobId { get; set; }

        public int FileId { get; set; }
        public int SeqNo { get; set; }

        public string FileName { get; set; }
        public string FilePath { get; set; }

        public string EnqueueMessage { get; set; }
    }

    /// <summary>
    /// 闪光/火花检测算法参数（针对“非饱和闪光 + 1秒内出现-消失”优化）
    /// </summary>
    public sealed class AlgoParams
    {
        // ===== 抽帧 =====
        public int SampleFps { get; set; } = 8;      // 建议 6~10，默认 8
        public int SampleEverySec { get; set; } = 1; // 兼容旧字段

        // ===== diff/轮廓候选 =====
        public int DiffThreshold { get; set; } = 35;
        public int DiffThresholdMin { get; set; } = 12;
        public double AdaptiveDiffK { get; set; } = 2.2;
        public double MinContourArea { get; set; } = 60;

        // ===== 全局亮度突变（你视频里这个才是“主信号”）=====
        public double MeanDeltaRise { get; set; } = 6.0;   // “上升”阈值（灰度均值差），建议 5~10
        public double MeanDeltaFall { get; set; } = 4.0;   // “回落”阈值（回到基线附近）

        // ===== 动态高亮比例（替代固定 240 饱和像素比例）=====
        public double BrightStdK { get; set; } = 2.0;      // 动态阈值 = mean + K*std
        public int BrightThrMin { get; set; } = 170;
        public int BrightThrMax { get; set; } = 250;

        public double BrightRatioDelta { get; set; } = 0.0012; // 你视频量级大约 0.0005~0.002，建议 0.001~0.002

        // ===== 闪光/火花判别辅助 =====
        public double FlashAreaRatio { get; set; } = 0.22;     // 大面积变化辅助（略放宽）
        public double GlobalBrightnessDelta { get; set; } = 12; // 作为“候选触发/闪光辅助”阈值（你视频里 globalDelta 能到 14+）

        // ===== 脉冲时序 =====
        public double MaxPulseSec { get; set; } = 1.3;      // 1秒内出现-消失，给点余量
        public double SustainRejectSec { get; set; } = 2.0;  // 持续光源抑制

        // ===== 预处理 =====
        public int ResizeMaxWidth { get; set; } = 640;
        public int BlurKernel { get; set; } = 5;

        // ===== 抑制策略 =====
        public int RequireConsecutiveHits { get; set; } = 1; // 先保证能出事件，再收紧到 2
        public int CooldownSec { get; set; } = 1;
        public int MergeGapSec { get; set; } = 2;
        public double MaxMotionRatioPerSec { get; set; } = 0.12;

        public static AlgoParams ParseOrDefault(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return new AlgoParams();

                var p = JsonConvert.DeserializeObject<AlgoParams>(json) ?? new AlgoParams();

                if (p.SampleFps < 0) p.SampleFps = 0;
                if (p.SampleEverySec <= 0) p.SampleEverySec = 1;

                if (p.DiffThreshold <= 0) p.DiffThreshold = 35;
                if (p.DiffThresholdMin <= 0) p.DiffThresholdMin = 12;
                if (p.AdaptiveDiffK < 0) p.AdaptiveDiffK = 0;

                if (p.MinContourArea < 1) p.MinContourArea = 60;

                if (p.MeanDeltaRise <= 0) p.MeanDeltaRise = 6.0;
                if (p.MeanDeltaFall <= 0) p.MeanDeltaFall = 4.0;

                if (p.BrightStdK <= 0) p.BrightStdK = 2.0;
                if (p.BrightThrMin <= 0) p.BrightThrMin = 170;
                if (p.BrightThrMax <= 0) p.BrightThrMax = 250;
                if (p.BrightThrMax < p.BrightThrMin) p.BrightThrMax = p.BrightThrMin;

                if (p.BrightRatioDelta <= 0) p.BrightRatioDelta = 0.0012;

                if (p.FlashAreaRatio <= 0) p.FlashAreaRatio = 0.22;
                if (p.GlobalBrightnessDelta <= 0) p.GlobalBrightnessDelta = 12;

                if (p.MaxPulseSec <= 0) p.MaxPulseSec = 1.3;
                if (p.SustainRejectSec <= 0) p.SustainRejectSec = 2.0;

                if (p.ResizeMaxWidth < 0) p.ResizeMaxWidth = 0;
                if (p.BlurKernel > 1 && p.BlurKernel % 2 == 0) p.BlurKernel += 1;

                if (p.RequireConsecutiveHits <= 0) p.RequireConsecutiveHits = 1;
                if (p.CooldownSec < 0) p.CooldownSec = 0;
                if (p.MergeGapSec < 0) p.MergeGapSec = 0;

                if (p.MaxMotionRatioPerSec < 0) p.MaxMotionRatioPerSec = 0.12;

                return p;
            }
            catch
            {
                return new AlgoParams();
            }
        }
    }

    public sealed class CreateJobResultDto
    {
        public int JobId { get; set; }
        public string JobNo { get; set; }
        public byte Status { get; set; }
        public int TotalVideoCount { get; set; }
    }

    public sealed class ReanalyzeVideoRequest
    {
        public int[] FileIds { get; set; }
    }

    public sealed class ReanalyzeResultDto
    {
        public int RequeuedCount { get; set; }
        public int ClearedEventCount { get; set; }
        public int ClearedSnapshotCount { get; set; }
    }

    public class JobDetailDto
    {
        public int JobId { get; set; }
        public string JobNo { get; set; }

        public byte Status { get; set; }
        public int Progress { get; set; }

        public string AlgoCode { get; set; }
        public string AlgoParamsJson { get; set; }

        public int TotalVideoCount { get; set; }
        public int FinishedVideoCount { get; set; }
        public int TotalEventCount { get; set; }

        public string ErrorMessage { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }

        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        public List<JobFileDto> Files { get; set; } = new List<JobFileDto>();

        public static JobDetailDto From(VideoAnalysisJob job, List<VideoAnalysisFile> files)
        {
            if (job == null) return null;

            var dto = new JobDetailDto
            {
                JobId = job.Id,
                JobNo = job.JobNo,

                Status = job.Status,
                Progress = job.Progress,

                AlgoCode = job.AlgoCode,
                AlgoParamsJson = job.AlgoParamsJson,

                TotalVideoCount = job.TotalVideoCount,
                FinishedVideoCount = job.FinishedVideoCount,
                TotalEventCount = job.TotalEventCount,

                ErrorMessage = job.ErrorMessage,

                StartTime = job.StartTime,
                FinishTime = job.FinishTime,

                CrtTime = job.CrtTime,
                UpdTime = job.UpdTime
            };

            if (files != null && files.Count > 0)
            {
                dto.Files = files
                    .OrderBy(x => x.SeqNo)
                    .Select(JobFileDto.From)
                    .Where(x => x != null)
                    .ToList();
            }

            return dto;
        }
    }

    public class JobFileDto
    {
        public int Id { get; set; }
        public int JobId { get; set; }

        public string FileName { get; set; }
        public string FilePath { get; set; }

        public int? DurationSec { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        public byte Status { get; set; }
        public string ErrorMessage { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        public static JobFileDto From(VideoAnalysisFile f)
        {
            if (f == null) return null;

            return new JobFileDto
            {
                Id = f.Id,
                JobId = f.JobId,
                FileName = f.FileName,
                FilePath = f.FilePath,
                DurationSec = f.DurationSec,
                Width = f.Width,
                Height = f.Height,
                Status = f.Status,
                ErrorMessage = f.ErrorMessage,
                SeqNo = f.SeqNo,
                CrtTime = f.CrtTime,
                UpdTime = f.UpdTime
            };
        }
    }

    public sealed class JobVideoDto
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public byte Status { get; set; }
        public string ErrorMessage { get; set; }
        public int? DurationSec { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int SeqNo { get; set; }
    }

    public class EventDto
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int VideoFileId { get; set; }

        public byte EventType { get; set; }

        public int StartTimeSec { get; set; }
        public int EndTimeSec { get; set; }
        public int PeakTimeSec { get; set; }

        public int FrameIndex { get; set; }
        public decimal Confidence { get; set; }

        public string BBoxJson { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        public static EventDto From(VideoAnalysisEvent e)
        {
            if (e == null) return null;

            return new EventDto
            {
                Id = e.Id,
                JobId = e.JobId,
                VideoFileId = e.VideoFileId,
                EventType = e.EventType,
                StartTimeSec = e.StartTimeSec,
                EndTimeSec = e.EndTimeSec,
                PeakTimeSec = e.PeakTimeSec,
                FrameIndex = e.FrameIndex,
                Confidence = e.Confidence,
                BBoxJson = e.BBoxJson,
                SeqNo = e.SeqNo,
                CrtTime = e.CrtTime,
                UpdTime = e.UpdTime
            };
        }
    }

     public class SnapshotDto
    {
        public int Id { get; set; }
        public int EventId { get; set; }

        public string ImagePath { get; set; }

        public int TimeSec { get; set; }
        public int FrameIndex { get; set; }

        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        public static SnapshotDto From(VideoAnalysisSnapshot s)
        {
            if (s == null) return null;

            return new SnapshotDto
            {
                Id = s.Id,
                EventId = s.EventId,
                ImagePath = s.ImagePath,
                TimeSec = s.TimeSec,
                FrameIndex = s.FrameIndex,
                ImageWidth = s.ImageWidth,
                ImageHeight = s.ImageHeight,
                SeqNo = s.SeqNo,
                CrtTime = s.CrtTime,
                UpdTime = s.UpdTime
            };
        }
    }

      public sealed class DeleteJobResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>物理文件删除失败列表（可选，用于排障）</summary>
        public string[] FailedFiles { get; set; }
    }
}
