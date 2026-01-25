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
    /// 说明：采样只认 SampleFps
    /// </summary>
    public sealed class AlgoParams
    {
         // ===== 抽帧 =====
        public int SampleFps { get; set; } = 8;      // 建议 6~10，默认 8

        // ===== diff/轮廓候选 =====
        public int DiffThreshold { get; set; } = 40;
        public int DiffThresholdMin { get; set; } = 14;
        public double AdaptiveDiffK { get; set; } = 2.6;
        public double MinContourArea { get; set; } = 70;

        // ===== 全局亮度突变 =====
        public double MeanDeltaRise { get; set; } = 6.0;
        public double MeanDeltaFall { get; set; } = 4.0;

        // ===== 动态高亮比例 =====
        public double BrightStdK { get; set; } = 2.0;      // thr = mean + K*std
        public int BrightThrMin { get; set; } = 170;
        public int BrightThrMax { get; set; } = 250;
        public double BrightRatioDelta { get; set; } = 0.0012;

        // ===== 闪光/火花判别辅助 =====
        public double FlashAreaRatio { get; set; } = 0.22;
        public double GlobalBrightnessDelta { get; set; } = 12;

        // ===== 脉冲时序 =====
        public double MaxPulseSec { get; set; } = 1.3;
        public double SustainRejectSec { get; set; } = 2.0;

        // ===== 预处理 =====
        public int ResizeMaxWidth { get; set; } = 640;
        public int BlurKernel { get; set; } = 5;

        // ===== 抑制策略 =====
        public int RequireConsecutiveHits { get; set; } = 1;
        public int CooldownSec { get; set; } = 1;
        public int MergeGapSec { get; set; } = 2;
        public double MaxMotionRatioPerSec { get; set; } = 0.12;

        public static AlgoParams ParseOrDefault(string json)
        {
            AlgoParams p;
            if (string.IsNullOrWhiteSpace(json))
                p = new AlgoParams();
            else
            {
                try
                {
                    p = JsonConvert.DeserializeObject<AlgoParams>(json) ?? new AlgoParams();
                }
                catch
                {
                    return new AlgoParams();
                }
            }

            // SampleFps：只认它
            if (p.SampleFps <= 0) p.SampleFps = 8;
            if (p.SampleFps > 60) p.SampleFps = 60;

            if (p.DiffThreshold <= 0) p.DiffThreshold = 35;
            if (p.DiffThreshold > 255) p.DiffThreshold = 255;

            if (p.DiffThresholdMin <= 0) p.DiffThresholdMin = 12;
            if (p.DiffThresholdMin > 255) p.DiffThresholdMin = 255;

            if (p.AdaptiveDiffK < 0) p.AdaptiveDiffK = 0;
            if (p.AdaptiveDiffK > 10) p.AdaptiveDiffK = 10;

            if (p.MinContourArea < 1) p.MinContourArea = 60;
            if (p.MinContourArea > 1_000_000) p.MinContourArea = 1_000_000;

            if (p.MeanDeltaRise <= 0) p.MeanDeltaRise = 6.0;
            if (p.MeanDeltaRise > 80) p.MeanDeltaRise = 80;

            if (p.MeanDeltaFall <= 0) p.MeanDeltaFall = 4.0;
            if (p.MeanDeltaFall > 80) p.MeanDeltaFall = 80;

            if (p.BrightStdK <= 0) p.BrightStdK = 2.0;
            if (p.BrightStdK > 10) p.BrightStdK = 10;

            if (p.BrightThrMin <= 0) p.BrightThrMin = 170;
            if (p.BrightThrMin > 255) p.BrightThrMin = 255;

            if (p.BrightThrMax <= 0) p.BrightThrMax = 250;
            if (p.BrightThrMax > 255) p.BrightThrMax = 255;

            if (p.BrightThrMax < p.BrightThrMin) p.BrightThrMax = p.BrightThrMin;

            if (p.BrightRatioDelta <= 0) p.BrightRatioDelta = 0.0012;
            if (p.BrightRatioDelta > 0.2) p.BrightRatioDelta = 0.2;

            if (p.FlashAreaRatio <= 0) p.FlashAreaRatio = 0.22;
            if (p.FlashAreaRatio > 1) p.FlashAreaRatio = 1;

            if (p.GlobalBrightnessDelta <= 0) p.GlobalBrightnessDelta = 12;
            if (p.GlobalBrightnessDelta > 120) p.GlobalBrightnessDelta = 120;

            if (p.MaxPulseSec <= 0) p.MaxPulseSec = 1.3;
            if (p.MaxPulseSec > 10) p.MaxPulseSec = 10;

            if (p.SustainRejectSec <= 0) p.SustainRejectSec = 2.0;
            if (p.SustainRejectSec > 30) p.SustainRejectSec = 30;

            if (p.ResizeMaxWidth < 0) p.ResizeMaxWidth = 0;
            if (p.ResizeMaxWidth > 4096) p.ResizeMaxWidth = 4096;

            if (p.BlurKernel <= 1) p.BlurKernel = 1;
            if (p.BlurKernel > 31) p.BlurKernel = 31;
            if (p.BlurKernel > 1 && p.BlurKernel % 2 == 0) p.BlurKernel += 1;

            if (p.RequireConsecutiveHits <= 0) p.RequireConsecutiveHits = 1;
            if (p.RequireConsecutiveHits > 10) p.RequireConsecutiveHits = 10;

            if (p.CooldownSec < 0) p.CooldownSec = 0;
            if (p.CooldownSec > 600) p.CooldownSec = 600;

            if (p.MergeGapSec < 0) p.MergeGapSec = 0;
            if (p.MergeGapSec > 600) p.MergeGapSec = 600;

            if (p.MaxMotionRatioPerSec < 0) p.MaxMotionRatioPerSec = 0.12;
            if (p.MaxMotionRatioPerSec > 1) p.MaxMotionRatioPerSec = 1;

            return p;
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
        public string AlgoParamsJson { get; set; }
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

        public int? AnalyzeSec { get; set; }
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
                AnalyzeSec = f.AnalyzeSec,
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

    public sealed class SnapshotDto
    {
        public int Id { get; set; }
        public int VideoFileId { get; set; }
        public int EventId { get; set; }
        public string ImagePath { get; set; }
        public int TimeSec { get; set; }
        public int FrameIndex { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int SeqNo { get; set; }

        public double Confidence { get; set; } // ★新增，前端更好用

        public static SnapshotDto From(VideoAnalysisSnapshot s)
        {
            if (s == null) return null;
            return new SnapshotDto
            {
                Id = s.Id,
                VideoFileId = s.VideoFileId,
                EventId = s.EventId,
                ImagePath = s.ImagePath,
                TimeSec = s.TimeSec,
                FrameIndex = s.FrameIndex,
                ImageWidth = s.ImageWidth,
                ImageHeight = s.ImageHeight,
                SeqNo = s.SeqNo,
                Confidence = (double)s.Confidence
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

    public sealed class VideoAnalysisWorkerOptions
    {
        /// <summary>
        /// 最大并发处理数（0=自动计算：min(CPU/2,4)，最少1）
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 0;

        /// <summary>
        /// Dequeue 异常退避（毫秒）
        /// </summary>
        public int DequeueErrorBackoffMs { get; set; } = 500;

        /// <summary>
        /// 单个文件处理超时（秒），0=不启用超时
        /// </summary>
        public int PerFileTimeoutSec { get; set; } = 0;

        /// <summary>
        /// 单个 fileId 最大重试次数（不含首次），0=不重试
        /// </summary>
        public int MaxRetryCount { get; set; } = 2;

        /// <summary>
        /// 重试初始退避（毫秒）
        /// </summary>
        public int RetryBaseDelayMs { get; set; } = 300;

        /// <summary>
        /// 重试最大退避（毫秒）
        /// </summary>
        public int RetryMaxDelayMs { get; set; } = 5000;

        /// <summary>
        /// 退避抖动比例（0~1），0.2 表示 ±20% 抖动
        /// </summary>
        public double RetryJitterRatio { get; set; } = 0.2;

        /// <summary>
        /// 处理异常后的轻微退避（毫秒），用于避免持续失败刷爆资源
        /// </summary>
        public int WorkErrorBackoffMs { get; set; } = 200;
    }
}
