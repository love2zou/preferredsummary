using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenCvSharp;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Video.Application.Dto;

namespace Preferred.Api.Services
{
    public sealed class SparkDetectionService
    {
        private readonly ApplicationDbContext _db;
        private readonly string _root;
        private readonly ILogger<SparkDetectionService> _logger;

        public SparkDetectionService(ApplicationDbContext db, ILogger<SparkDetectionService> logger, Microsoft.Extensions.Options.IOptions<FileStorageConfig> fs)
        {
            _db = db;
            _root = fs.Value.VideoRootPath;
            _logger = logger;
        }

        /// <summary>
        /// 新模式：处理单个视频文件（fileId 出队）
        /// </summary>
        public async Task ProcessFileAsync(int fileId, CancellationToken ct)
        {
            var vf = await _db.VideoAnalysisFiles.FirstOrDefaultAsync(x => x.Id == fileId, ct);
            if (vf == null) return;

            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.Id == vf.JobId, ct);
            if (job == null) return;

            // job cancelled/done/failed => 不处理
            if (job.Status == 4 || job.Status == 2 || job.Status == 3) return;

            // file already done/failed => 防重
            if (vf.Status == 2 || vf.Status == 3) return;

            // 标记 processing
            vf.Status = 1;
            vf.UpdTime = DateTime.Now;
            await _db.SaveChangesAsync(ct);

            var sw = Stopwatch.StartNew();

            try
            {
                var algo = AlgoParams.ParseOrDefault(job.AlgoParamsJson);

                var (eventCount, durationSec, width, height) = ProcessSingleVideo(job, vf, algo);

                vf.DurationSec = durationSec;
                vf.Width = width;
                vf.Height = height;

                // 如果你实体没有这些字段，请删除
                vf.EventCount = eventCount;
                vf.AnalyzeMs = (int)sw.Elapsed.TotalSeconds;

                vf.Status = 2;
                vf.ErrorMessage = null;
                vf.UpdTime = DateTime.Now;

                await _db.SaveChangesAsync(ct);

                await UpdateJobAggregateAsync(job.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessFile failed. fileId={FileId}", fileId);

                vf.Status = 3;
                vf.ErrorMessage = ex.Message;

                // 如果你实体没有该字段，请删除
                vf.AnalyzeMs = (int)sw.ElapsedMilliseconds;

                vf.UpdTime = DateTime.Now;
                await _db.SaveChangesAsync(ct);

                await UpdateJobAggregateAsync(job.Id, ct);
                throw;
            }
        }

        private async Task UpdateJobAggregateAsync(int jobId, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId, ct);
            if (job == null) return;

            if (job.Status == 4) return; // cancelled
            if (job.Status == 2 || job.Status == 3) return;

            var files = await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.JobId == jobId)
                .Select(x => new { x.Status })
                .ToListAsync(ct);

            int totalUploaded = files.Count;
            int finished = files.Count(x => x.Status == 2);
            int failed = files.Count(x => x.Status == 3);

            // 事件数聚合
            int totalEvents = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == jobId)
                .CountAsync(ct);

            job.FinishedVideoCount = finished;
            job.TotalEventCount = totalEvents;
            job.UpdTime = DateTime.Now;

            // 关键：Close 后用 TotalVideoCount 作为固定总数；未 Close 则用已上传数做一个“动态进度”
            if (job.TotalVideoCount > 0)
            {
                // 已 Close
                job.Progress = job.TotalVideoCount <= 0 ? 0 : (int)Math.Round(finished * 100.0 / job.TotalVideoCount);

                if (finished + failed >= job.TotalVideoCount)
                {
                    job.Status = 2; // done
                    job.Progress = 100;
                    job.FinishTime = DateTime.Now;
                }
            }
            else
            {
                // 未 Close：只能按“当前已上传数”估算进度，不置 done
                job.Progress = totalUploaded <= 0 ? 0 : (int)Math.Round(finished * 100.0 / totalUploaded);
                // job.TotalVideoCount 不要在这里写，保持 0 表示“未知结束”
            }

            await _db.SaveChangesAsync(ct);
        }

        private (int eventCount, int? durationSec, int? width, int? height) ProcessSingleVideo(
            VideoAnalysisJob job,
            VideoAnalysisFile vf,
            AlgoParams algo)
        {
            using var cap = new VideoCapture(vf.FilePath);
            if (!cap.IsOpened())
                throw new InvalidOperationException("VideoCapture open failed: " + vf.FilePath);

            int fps = (int)Math.Round(cap.Fps);
            if (fps <= 0) fps = 25;

            int sampleEveryFrames = Math.Max(1, algo.SampleEverySec * fps);

            int w = (int)cap.FrameWidth;
            int h = (int)cap.FrameHeight;

            int frameIndex = 0;
            int eventSeq = 0;
            int snapshotSeq = 0;
            int found = 0;

            using var prev = new Mat();
            using var curr = new Mat();

            bool ok = cap.Read(prev);
            if (!ok || prev.Empty())
                return (0, null, w, h);

            while (cap.Read(curr))
            {
                frameIndex++;

                if (curr.Empty()) break;
                if (frameIndex % sampleEveryFrames != 0) continue;

                if (TryDetect(prev, curr, algo, out var bbox, out var isFlash, out var confidence))
                {
                    found++;

                    int timeSec = frameIndex / fps;
                    byte eventType = (byte)(isFlash ? 1 : 2);

                    var jobDir = Path.Combine(_root, job.JobNo);
                    var snapDir = Path.Combine(jobDir, "snapshots");
                    Directory.CreateDirectory(snapDir);

                    using var boxed = curr.Clone();
                    Cv2.Rectangle(boxed, bbox, isFlash ? Scalar.Yellow : Scalar.Red, 2);
                    var label = isFlash ? "FLASH" : "SPARK";
                    Cv2.PutText(boxed, $"{label} t={timeSec}s", new Point(10, 30),
                        HersheyFonts.HersheySimplex, 1.0, isFlash ? Scalar.Yellow : Scalar.Red, 2);

                    string imagePath = Path.Combine(snapDir, $"{vf.Id}_{frameIndex}_{label}.jpg");
                    Cv2.ImWrite(imagePath, boxed);

                    eventSeq++;
                    var ev = new VideoAnalysisEvent
                    {
                        JobId = job.Id,
                        VideoFileId = vf.Id,
                        EventType = eventType,
                        StartTimeSec = timeSec,
                        EndTimeSec = timeSec,
                        PeakTimeSec = timeSec,
                        FrameIndex = frameIndex,
                        Confidence = (decimal)confidence,
                        BBoxJson = JsonConvert.SerializeObject(new { x = bbox.X, y = bbox.Y, w = bbox.Width, h = bbox.Height }),
                        SeqNo = eventSeq,
                        CrtTime = DateTime.Now,
                        UpdTime = DateTime.Now
                    };

                    _db.VideoAnalysisEvents.Add(ev);
                    _db.SaveChanges();

                    snapshotSeq++;
                    var snap = new VideoAnalysisSnapshot
                    {
                        EventId = ev.Id,
                        ImagePath = imagePath,
                        TimeSec = timeSec,
                        FrameIndex = frameIndex,
                        ImageWidth = boxed.Width,
                        ImageHeight = boxed.Height,
                        SeqNo = snapshotSeq,
                        CrtTime = DateTime.Now,
                        UpdTime = DateTime.Now
                    };

                    _db.VideoAnalysisSnapshots.Add(snap);
                    _db.SaveChanges();
                }

                curr.CopyTo(prev);
            }

            int? durationSec = cap.FrameCount > 0 && cap.Fps > 0
                ? (int?)Math.Round(cap.FrameCount / cap.Fps)
                : null;

            return (found, durationSec, w, h);
        }

        private static bool TryDetect(Mat prev, Mat curr, AlgoParams algo, out Rect bbox, out bool isFlash, out double confidence)
        {
            bbox = default;
            isFlash = false;
            confidence = 0;

            using var g1 = new Mat();
            using var g2 = new Mat();
            Cv2.CvtColor(prev, g1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(curr, g2, ColorConversionCodes.BGR2GRAY);

            double mean1 = Cv2.Mean(g1).Val0;
            double mean2 = Cv2.Mean(g2).Val0;
            double globalDelta = Math.Abs(mean2 - mean1);

            using var diff = new Mat();
            Cv2.Absdiff(g1, g2, diff);
            Cv2.Threshold(diff, diff, algo.DiffThreshold, 255, ThresholdTypes.Binary);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(diff, diff, MorphTypes.Open, kernel);

            Cv2.FindContours(diff, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours == null || contours.Length == 0) return false;

            var max = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            double area = Cv2.ContourArea(max);
            if (area < algo.MinContourArea) return false;

            bbox = Cv2.BoundingRect(max);

            double frameArea = Math.Max(1.0, curr.Width * curr.Height);
            double areaRatio = area / frameArea;

            isFlash = areaRatio >= algo.FlashAreaRatio || globalDelta >= algo.GlobalBrightnessDelta;

            double score = Math.Min(1.0, (areaRatio * 4.0) + (globalDelta / 100.0));
            confidence = Math.Max(0.1, Math.Min(1.0, score));

            return true;
        }
    }
}