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

    public sealed class AlgoParams
    {
        /// <summary>每隔多少秒抽一帧（默认1秒1帧）</summary>
        public int SampleEverySec { get; set; } = 1;

        /// <summary>帧差二值化阈值（越大越不敏感）</summary>
        public int DiffThreshold { get; set; } = 40;

        /// <summary>最小轮廓面积（像素），过滤噪声</summary>
        public double MinContourArea { get; set; } = 60;

        /// <summary>最大轮廓面积占比（用于区分全局闪光）</summary>
        public double FlashAreaRatio { get; set; } = 0.25; // 超过画面25%更像闪光

        /// <summary>全局亮度变化阈值（用于闪光判定）</summary>
        public double GlobalBrightnessDelta { get; set; } = 25;

        public static AlgoParams ParseOrDefault(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new AlgoParams();
            try
            {
                return JsonConvert.DeserializeObject<AlgoParams>(json) ?? new AlgoParams();
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
