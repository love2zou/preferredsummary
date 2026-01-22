using System;
using System.Collections.Generic;
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
    /// <summary>
    /// 闪光/火花检测服务（峰值对齐截图版）
    /// 核心特性：
    /// 1) 每个采样帧都入 window（保证回落帧存在）
    /// 2) pending 观察期满再 ConfirmPulse（避免“永远等不到回落”）
    /// 3) ring buffer 缓存最近几秒采样帧，按 peakTimeMs 取最接近帧做截图（时间点对齐）
    /// 4) resize 检测的 bbox 映射回原图坐标（截图框不偏）
    /// </summary>
    public sealed class SparkDetectionService
    {
        private readonly ApplicationDbContext _db;
        private readonly string _root;
        private readonly ILogger<SparkDetectionService> _logger;

        public SparkDetectionService(
            ApplicationDbContext db,
            ILogger<SparkDetectionService> logger,
            Microsoft.Extensions.Options.IOptions<FileStorageConfig> fs)
        {
            _db = db;
            _root = fs.Value.VideoRootPath;
            _logger = logger;
        }

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

                var (eventCount, durationSec, width, height, analyzeMs) = await ProcessSingleVideoAsync(job, vf, algo, ct);

                vf.DurationSec = durationSec;
                vf.Width = width;
                vf.Height = height;

                // 如果你实体没有这些字段，请删除
                vf.EventCount = eventCount;
                vf.AnalyzeMs = (int)analyzeMs;

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
                vf.AnalyzeMs = (int)sw.Elapsed.TotalMilliseconds;

                vf.UpdTime = DateTime.Now;
                await _db.SaveChangesAsync(ct);

                await UpdateJobAggregateAsync(vf.JobId, ct);
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

            int totalEvents = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == jobId)
                .CountAsync(ct);

            job.FinishedVideoCount = finished;
            job.TotalEventCount = totalEvents;
            job.UpdTime = DateTime.Now;

            if (job.TotalVideoCount > 0)
            {
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
                job.Progress = totalUploaded <= 0 ? 0 : (int)Math.Round(finished * 100.0 / totalUploaded);
            }

            await _db.SaveChangesAsync(ct);
        }

        // =========================
        // Frame ring buffer: 按 peakTimeMs 截取最接近帧
        // =========================
        private sealed class FrameBufferItem
        {
            public int TimeMs { get; set; }
            public int FrameIndex { get; set; }
            public Mat Frame { get; set; } // 原图分辨率 Clone
        }

        private sealed class FrameRingBuffer : IDisposable
        {
            private readonly int _maxItems;
            private readonly Queue<FrameBufferItem> _q = new Queue<FrameBufferItem>();

            public FrameRingBuffer(int maxItems)
            {
                _maxItems = Math.Max(3, maxItems);
            }

            public void Add(int timeMs, int frameIndex, Mat cloneFrame)
            {
                _q.Enqueue(new FrameBufferItem { TimeMs = timeMs, FrameIndex = frameIndex, Frame = cloneFrame });
                while (_q.Count > _maxItems)
                {
                    var old = _q.Dequeue();
                    try { old.Frame?.Dispose(); } catch { }
                }
            }

            public void TrimByTime(int keepMs)
            {
                if (_q.Count == 0) return;

                int newest = 0;
                foreach (var it in _q) if (it.TimeMs > newest) newest = it.TimeMs;

                while (_q.Count > 0)
                {
                    var head = _q.Peek();
                    if (newest - head.TimeMs <= keepMs) break;

                    var old = _q.Dequeue();
                    try { old.Frame?.Dispose(); } catch { }
                }
            }

            public FrameBufferItem GetNearest(int targetTimeMs)
            {
                FrameBufferItem best = null;
                int bestAbs = int.MaxValue;

                foreach (var it in _q)
                {
                    int d = Math.Abs(it.TimeMs - targetTimeMs);
                    if (d < bestAbs)
                    {
                        bestAbs = d;
                        best = it;
                    }
                }

                return best;
            }

            public void Dispose()
            {
                while (_q.Count > 0)
                {
                    var it = _q.Dequeue();
                    try { it.Frame?.Dispose(); } catch { }
                }
            }
        }

        private sealed class SamplePoint
        {
            public int FrameIndex { get; set; }
            public int TimeMs { get; set; }

            public double Mean { get; set; }
            public double Std { get; set; }

            public double MeanDelta { get; set; }     // abs(mean2-mean1)
            public double BrightRatio { get; set; }   // 动态高亮比例
            public double BrightDelta { get; set; }   // abs(br2-br1)

            public bool Candidate { get; set; }

            /// <summary>bbox 统一是“原图坐标”</summary>
            public Rect BBox { get; set; }
            public double AreaRatio { get; set; }
            public double Confidence { get; set; }
            public Point2d Center { get; set; } // 原图坐标
        }

        private async Task<(int eventCount, int? durationSec, int? width, int? height, double analyzeMs)> ProcessSingleVideoAsync(
            VideoAnalysisJob job,
            VideoAnalysisFile vf,
            AlgoParams algo,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            using var cap = new VideoCapture(vf.FilePath);
            if (!cap.IsOpened())
                throw new InvalidOperationException("VideoCapture open failed: " + vf.FilePath);

            int fps = (int)Math.Round(cap.Fps);
            if (fps <= 0) fps = 25;

            int sampleEveryFrames = CalcSampleEveryFrames(fps, algo);

            int w0 = (int)cap.FrameWidth;
            int h0 = (int)cap.FrameHeight;

            int consecutiveHits = 0;
            int lastEventFrame = -999999;

            bool pendingPulse = false;
            int pendingStartMs = 0;
            int pendingLastHitMs = 0;

            var window = new Queue<SamplePoint>();
            int windowMaxMs = 3200;
            int pulseMaxMs = (int)Math.Ceiling(Math.Max(900, algo.MaxPulseSec * 1000));

            VideoAnalysisEvent openEvent = null;
            int eventSeq = 0;
            int snapshotSeq = 0;

            var jobDir = Path.Combine(_root, job.JobNo);
            var snapDir = Path.Combine(jobDir, "snapshots");
            Directory.CreateDirectory(snapDir);

            using var prev = new Mat();
            using var curr = new Mat();

            int frameCounter = 0;
            if (!cap.Read(prev) || prev.Empty())
                return (0, null, w0, h0, sw.Elapsed.TotalMilliseconds);

            // ring buffer：缓存最近 3~3.5 秒采样帧
            int sampleFps = algo.SampleFps > 0 ? algo.SampleFps : Math.Max(1, fps / sampleEveryFrames);
            using var frameBuf = new FrameRingBuffer(maxItems: Math.Max(24, sampleFps * 4));

            int foundEvents = 0;

            while (cap.Read(curr))
            {
                ct.ThrowIfCancellationRequested();

                frameCounter++;
                if (curr.Empty()) break;

                if (frameCounter % sampleEveryFrames != 0)
                    continue;

                int timeMs = (int)Math.Round(frameCounter * 1000.0 / fps);

                // 缓存“原图帧”
                frameBuf.Add(timeMs, frameCounter, curr.Clone());
                frameBuf.TrimByTime(keepMs: 3600);

                // 1) 每个采样帧都入 window：统计点（含回落）
                var stats = BuildStatsPoint(prev, curr, algo);
                stats.FrameIndex = frameCounter;
                stats.TimeMs = timeMs;

                // 2) 候选检测（全局突变 or diff/contour），输出原图 bbox
                bool isCandidate = TryDetectCandidate(prev, curr, algo, stats, out var cand);
                if (isCandidate && cand != null)
                {
                    stats.Candidate = true;
                    stats.BBox = cand.BBox;
                    stats.AreaRatio = cand.AreaRatio;
                    stats.Confidence = cand.Confidence;
                    stats.Center = cand.Center;
                }
                else
                {
                    // 全局候选：没有 bbox 也可以进入 pending
                    stats.Candidate = isCandidate;
                }

                EnqueueWindow(window, stats, windowMaxMs);

                // 3) 进入 pending
                if (isCandidate)
                {
                    consecutiveHits++;

                    if (consecutiveHits >= Math.Max(1, algo.RequireConsecutiveHits))
                    {
                        int cooldownFrames = Math.Max(0, algo.CooldownSec) * fps;
                        if (frameCounter - lastEventFrame >= cooldownFrames)
                        {
                            if (!pendingPulse)
                            {
                                pendingPulse = true;
                                pendingStartMs = timeMs;
                            }
                            pendingLastHitMs = timeMs;
                        }
                    }
                }
                else
                {
                    consecutiveHits = 0;
                }

                // pending 超时放弃
                if (pendingPulse && timeMs - pendingLastHitMs > Math.Max(1200, pulseMaxMs + 450))
                {
                    pendingPulse = false;
                    consecutiveHits = 0;
                    TrimWindowToRecent(window, keepMs: 1500);
                }

                // 4) 观察期满再确认短脉冲
                if (pendingPulse && (timeMs - pendingStartMs >= pulseMaxMs))
                {
                    if (ConfirmPulse(
                        window,
                        algo,
                        pulseMaxMs,
                        out bool isFlash,
                        out double conf,
                        out Rect bestBox,
                        out int peakTimeMs,
                        out bool sustainReject,
                        out bool motionReject))
                    {
                        if (!sustainReject && !motionReject)
                        {
                            // 用 peakTimeMs 在 ring buffer 中取最接近帧截图
                            var peakFrame = frameBuf.GetNearest(peakTimeMs);
                            int snapTimeMs = peakFrame?.TimeMs ?? peakTimeMs;
                            int snapFrameIndex = peakFrame?.FrameIndex ?? frameCounter;

                            int timeSec = (int)Math.Round(snapTimeMs / 1000.0);
                            byte eventType = (byte)(isFlash ? 1 : 2);

                            // 合并：同类型短间隔合并
                            bool merged = false;
                            if (openEvent != null &&
                                openEvent.VideoFileId == vf.Id &&
                                openEvent.EventType == eventType &&
                                (timeSec - openEvent.EndTimeSec) <= Math.Max(0, algo.MergeGapSec))
                            {
                                openEvent.EndTimeSec = timeSec;
                                openEvent.PeakTimeSec = timeSec;
                                openEvent.FrameIndex = snapFrameIndex;
                                openEvent.Confidence = Math.Max(openEvent.Confidence, (decimal)conf);
                                openEvent.BBoxJson = JsonConvert.SerializeObject(new { x = bestBox.X, y = bestBox.Y, w = bestBox.Width, h = bestBox.Height });
                                openEvent.UpdTime = DateTime.Now;

                                _db.VideoAnalysisEvents.Update(openEvent);
                                await _db.SaveChangesAsync(ct);
                                merged = true;
                            }

                            if (!merged)
                            {
                                foundEvents++;
                                eventSeq++;

                                openEvent = new VideoAnalysisEvent
                                {
                                    JobId = job.Id,
                                    VideoFileId = vf.Id,
                                    EventType = eventType,
                                    StartTimeSec = timeSec,
                                    EndTimeSec = timeSec,
                                    PeakTimeSec = timeSec,
                                    FrameIndex = snapFrameIndex,
                                    Confidence = (decimal)conf,
                                    BBoxJson = JsonConvert.SerializeObject(new { x = bestBox.X, y = bestBox.Y, w = bestBox.Width, h = bestBox.Height }),
                                    SeqNo = eventSeq,
                                    CrtTime = DateTime.Now,
                                    UpdTime = DateTime.Now
                                };

                                _db.VideoAnalysisEvents.Add(openEvent);
                                await _db.SaveChangesAsync(ct);
                            }

                            // 保存截图：用峰值附近帧
                            snapshotSeq++;
                            string label = isFlash ? "FLASH" : "SPARK";
                            string imagePath = Path.Combine(snapDir, $"{vf.Id}_{snapFrameIndex}_{label}_t{timeSec}s.jpg");

                            using (var boxed = (peakFrame?.Frame != null ? peakFrame.Frame.Clone() : curr.Clone()))
                            {
                                if (bestBox.Width > 0 && bestBox.Height > 0)
                                    Cv2.Rectangle(boxed, bestBox, isFlash ? Scalar.Yellow : Scalar.Red, 2);

                                Cv2.PutText(boxed, $"{label} t={timeSec}s conf={conf:0.00}", new Point(10, 30),
                                    HersheyFonts.HersheySimplex, 1.0, isFlash ? Scalar.Yellow : Scalar.Red, 2);

                                Cv2.ImWrite(imagePath, boxed);

                                var snap = new VideoAnalysisSnapshot
                                {
                                    EventId = openEvent.Id,
                                    ImagePath = imagePath,
                                    TimeSec = timeSec,
                                    FrameIndex = snapFrameIndex,
                                    ImageWidth = boxed.Width,
                                    ImageHeight = boxed.Height,
                                    SeqNo = snapshotSeq,
                                    CrtTime = DateTime.Now,
                                    UpdTime = DateTime.Now
                                };

                                _db.VideoAnalysisSnapshots.Add(snap);
                                await _db.SaveChangesAsync(ct);
                            }

                            lastEventFrame = snapFrameIndex;
                        }
                    }

                    // 结束 pending
                    pendingPulse = false;
                    consecutiveHits = 0;

                    TrimWindowToRecent(window, keepMs: 1500);
                }

                curr.CopyTo(prev);
            }

            int? durationSec = cap.FrameCount > 0 && cap.Fps > 0
                ? (int?)Math.Round(cap.FrameCount / cap.Fps)
                : null;

            sw.Stop();
            return (foundEvents, durationSec, w0, h0, sw.Elapsed.TotalMilliseconds);
        }

        private static int CalcSampleEveryFrames(int fps, AlgoParams algo)
        {
            if (algo.SampleFps > 0)
                return Math.Max(1, (int)Math.Round(fps * 1.0 / algo.SampleFps));

            return Math.Max(1, algo.SampleEverySec * fps);
        }

        /// <summary>
        /// 统计点：MeanDelta + 动态高亮比例（thr = mean + K*std clamp）
        /// </summary>
        private static SamplePoint BuildStatsPoint(Mat prev, Mat curr, AlgoParams algo)
        {
            using var p = ResizeIfNeeded(prev, algo.ResizeMaxWidth, out _);
            using var c = ResizeIfNeeded(curr, algo.ResizeMaxWidth, out _);

            using var g1 = new Mat();
            using var g2 = new Mat();
            Cv2.CvtColor(p, g1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(c, g2, ColorConversionCodes.BGR2GRAY);

            int k = algo.BlurKernel <= 1 ? 1 : (algo.BlurKernel % 2 == 1 ? algo.BlurKernel : algo.BlurKernel + 1);
            if (k >= 3)
            {
                Cv2.GaussianBlur(g1, g1, new Size(k, k), 0);
                Cv2.GaussianBlur(g2, g2, new Size(k, k), 0);
            }

            Cv2.MeanStdDev(g1, out var m1, out _);
            Cv2.MeanStdDev(g2, out var m2, out var s2);

            double mean1 = m1.Val0;
            double mean2 = m2.Val0;
            double std2 = s2.Val0;

            double meanDelta = Math.Abs(mean2 - mean1);

            int thr2 = (int)Math.Round(mean2 + algo.BrightStdK * std2);
            if (thr2 < algo.BrightThrMin) thr2 = algo.BrightThrMin;
            if (thr2 > algo.BrightThrMax) thr2 = algo.BrightThrMax;

            double br1 = CalcRatioAboveThreshold(g1, thr2);
            double br2 = CalcRatioAboveThreshold(g2, thr2);
            double brightDelta = Math.Abs(br2 - br1);

            return new SamplePoint
            {
                Mean = mean2,
                Std = std2,
                MeanDelta = meanDelta,
                BrightRatio = br2,
                BrightDelta = brightDelta,
                Candidate = false,
                BBox = default,
                AreaRatio = 0,
                Confidence = 0,
                Center = default
            };
        }

        /// <summary>
        /// 候选触发：三路任一路满足即可触发
        /// 1) MeanDelta 足够大（全局突亮/突暗）
        /// 2) BrightDelta 足够大（动态高亮比例突变）
        /// 3) diff + contour（局部变化）
        /// 其中 diff/contour 的 bbox 会从 resize 坐标映射到原图坐标
        /// </summary>
        private static bool TryDetectCandidate(Mat prev, Mat curr, AlgoParams algo, SamplePoint stats, out SamplePoint cand)
        {
            cand = null;

            // 全局候选：不依赖 bbox
            if (stats.MeanDelta >= algo.GlobalBrightnessDelta || stats.BrightDelta >= algo.BrightRatioDelta)
            {
                return true;
            }

            // diff/contour：在 resize 图上做
            using var p = ResizeIfNeeded(prev, algo.ResizeMaxWidth, out _);
            using var c = ResizeIfNeeded(curr, algo.ResizeMaxWidth, out double scaleC);

            using var g1 = new Mat();
            using var g2 = new Mat();
            Cv2.CvtColor(p, g1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(c, g2, ColorConversionCodes.BGR2GRAY);

            int k = algo.BlurKernel <= 1 ? 1 : (algo.BlurKernel % 2 == 1 ? algo.BlurKernel : algo.BlurKernel + 1);
            if (k >= 3)
            {
                Cv2.GaussianBlur(g1, g1, new Size(k, k), 0);
                Cv2.GaussianBlur(g2, g2, new Size(k, k), 0);
            }

            using var diff = new Mat();
            Cv2.Absdiff(g1, g2, diff);

            double thr = algo.DiffThreshold;
            if (algo.AdaptiveDiffK > 0)
            {
                Cv2.MeanStdDev(diff, out var m, out var sd);
                thr = Math.Max(algo.DiffThresholdMin, m.Val0 + algo.AdaptiveDiffK * sd.Val0);
            }
            thr = Math.Max(1, Math.Min(255, thr));
            Cv2.Threshold(diff, diff, thr, 255, ThresholdTypes.Binary);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(diff, diff, MorphTypes.Open, kernel);

            Cv2.FindContours(diff, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours == null || contours.Length == 0) return false;

            int maxIdx = 0;
            double maxArea = 0;
            for (int i = 0; i < contours.Length; i++)
            {
                double a = Cv2.ContourArea(contours[i]);
                if (a > maxArea)
                {
                    maxArea = a;
                    maxIdx = i;
                }
            }

            if (maxArea < Math.Max(1.0, algo.MinContourArea))
                return false;

            var bboxSmall = Cv2.BoundingRect(contours[maxIdx]);

            // 映射回原图坐标：original = small / scale
            double s = scaleC;
            if (s <= 0) s = 1.0;

            int x = (int)Math.Round(bboxSmall.X / s);
            int y = (int)Math.Round(bboxSmall.Y / s);
            int w = Math.Max(1, (int)Math.Round(bboxSmall.Width / s));
            int h = Math.Max(1, (int)Math.Round(bboxSmall.Height / s));

            var bbox = new Rect(x, y, w, h);
            bbox = ClampRect(bbox, curr.Width, curr.Height);

            double frameArea = Math.Max(1.0, curr.Width * curr.Height);
            double areaRatio = (bbox.Width * bbox.Height) / frameArea;

            var center = new Point2d(bbox.X + bbox.Width / 2.0, bbox.Y + bbox.Height / 2.0);

            double score =
                Math.Min(1.0, areaRatio * 5.0) * 0.40 +
                Math.Min(1.0, stats.MeanDelta / 20.0) * 0.40 +
                Math.Min(1.0, stats.BrightDelta / 0.006) * 0.20;

            cand = new SamplePoint
            {
                BBox = bbox,
                AreaRatio = areaRatio,
                Confidence = Math.Max(0.1, Math.Min(1.0, score)),
                Center = center
            };

            return true;
        }

        /// <summary>
        /// 短脉冲确认（主信号：MeanDelta；辅信号：BrightDelta）
        /// - 快上升（MeanDeltaRise 或 BrightRatioDelta 达标）
        /// - 快回落（窗口内回到 baseline 附近）
        /// - 持续短（<= MaxPulseSec）
        /// 抑制：
        /// - 持续高亮（>= SustainRejectSec）
        /// - 移动光源（中心位移过大，仅对有 bbox 的候选生效）
        /// </summary>
        private static bool ConfirmPulse(
            Queue<SamplePoint> window,
            AlgoParams algo,
            int pulseMaxMs,
            out bool isFlash,
            out double confidence,
            out Rect bestBox,
            out int peakTimeMs,
            out bool sustainReject,
            out bool motionReject)
        {
            isFlash = false;
            confidence = 0;
            bestBox = default;
            peakTimeMs = 0;
            sustainReject = false;
            motionReject = false;

            if (window == null || window.Count < 2)
                return false;

            var arr = window.ToArray();
            Array.Sort(arr, (a, b) => a.TimeMs.CompareTo(b.TimeMs));
            int n = arr.Length;

            // baseline：最早 2 个点
            int baseCount = n >= 2 ? 2 : 1;
            double baseMean = 0;
            double baseBR = 0;
            for (int i = 0; i < baseCount; i++)
            {
                baseMean += arr[i].Mean;
                baseBR += arr[i].BrightRatio;
            }
            baseMean /= baseCount;
            baseBR /= baseCount;

            // peak：以 Mean 最大点作为峰值（更贴合“全局突亮”）
            int peakIdx = 0;
            double peakMean = arr[0].Mean;
            for (int i = 1; i < n; i++)
            {
                if (arr[i].Mean > peakMean)
                {
                    peakMean = arr[i].Mean;
                    peakIdx = i;
                }
            }

            var peak = arr[peakIdx];
            peakTimeMs = peak.TimeMs;

            // bestBox：尽量取峰值附近、且 bbox 有效的点；否则用 peak 的 bbox（可能为空）
            bestBox = peak.BBox;
            if (bestBox.Width <= 0 || bestBox.Height <= 0)
            {
                // 向前后找最近的有效 bbox
                int bestJ = -1;
                int bestAbs = int.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (arr[j].BBox.Width <= 0 || arr[j].BBox.Height <= 0) continue;
                    int d = Math.Abs(arr[j].TimeMs - peakTimeMs);
                    if (d < bestAbs)
                    {
                        bestAbs = d;
                        bestJ = j;
                    }
                }
                if (bestJ >= 0) bestBox = arr[bestJ].BBox;
            }

            // rise：任一满足即可
            bool hasRise = false;
            for (int i = 0; i < n; i++)
            {
                if (arr[i].MeanDelta >= algo.MeanDeltaRise || arr[i].BrightDelta >= algo.BrightRatioDelta)
                {
                    hasRise = true;
                    break;
                }
            }
            if (!hasRise) return false;

            // fall：峰值后 pulseMaxMs 内回到 baseline 附近
            double meanBackTo = baseMean + algo.MeanDeltaFall;
            double brBackTo = baseBR + Math.Max(0.0005, algo.BrightRatioDelta * 0.8);

            bool hasFall = false;
            int fallEnd = peakTimeMs + pulseMaxMs;
            for (int i = peakIdx; i < n; i++)
            {
                int t = arr[i].TimeMs;
                if (t > fallEnd) break;

                if (arr[i].Mean <= meanBackTo && arr[i].BrightRatio <= brBackTo)
                {
                    hasFall = true;
                    break;
                }
            }
            if (!hasFall) return false;

            // 高亮持续时间：Mean 超过 baseline+MeanDeltaRise 的跨度
            int firstHigh = -1, lastHigh = -1;
            double highMeanThr = baseMean + algo.MeanDeltaRise;

            for (int i = 0; i < n; i++)
            {
                if (arr[i].Mean >= highMeanThr)
                {
                    if (firstHigh < 0) firstHigh = arr[i].TimeMs;
                    lastHigh = arr[i].TimeMs;
                }
            }

            int highSpanMs = (firstHigh >= 0 && lastHigh >= 0) ? (lastHigh - firstHigh) : 0;

            // 持续光源抑制
            sustainReject = (highSpanMs >= (int)(algo.SustainRejectSec * 1000));

            // motionReject：只有在窗口中有足够 bbox 点才判
            motionReject = CheckMotionReject(arr, peakTimeMs, algo);

            // 脉冲短
            int maxPulseMs = (int)(algo.MaxPulseSec * 1000);
            if (highSpanMs > Math.Max(250, maxPulseMs)) return false;

            // isFlash：均值提升足够大，或面积占比足够大
            double meanGain = Math.Max(0, peak.Mean - baseMean);
            bool flashByMean = meanGain >= Math.Max(algo.MeanDeltaRise, algo.GlobalBrightnessDelta * 0.8);
            bool flashByArea = peak.AreaRatio >= algo.FlashAreaRatio;
            isFlash = flashByMean || flashByArea;

            // 置信度：均值提升 + brightDelta + area
            double score =
                Math.Min(1.0, meanGain / 25.0) * 0.55 +
                Math.Min(1.0, peak.BrightDelta / 0.006) * 0.20 +
                Math.Min(1.0, peak.AreaRatio * 4.0) * 0.15 +
                (hasFall ? 0.10 : 0.0);

            confidence = Math.Max(0.1, Math.Min(1.0, score));
            return true;
        }

        /// <summary>
        /// 移动光源抑制：在 peak 前后 1s 内，bbox 中心累计位移超过画面宽度比例则拒绝
        /// 注意：如果窗口内有效 bbox 点很少（例如全局闪光），不拒绝
        /// </summary>
        private static bool CheckMotionReject(SamplePoint[] arr, int peakTimeMs, AlgoParams algo)
        {
            if (arr == null || arr.Length < 3) return false;

            int t0 = peakTimeMs - 1000;
            int t1 = peakTimeMs + 1000;

            // 收集 bbox 有效点
            var pts = new List<SamplePoint>(16);
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].TimeMs < t0) continue;
                if (arr[i].TimeMs > t1) break;

                if (arr[i].BBox.Width > 0 && arr[i].BBox.Height > 0)
                    pts.Add(arr[i]);
            }

            // 有效 bbox 点太少，不做运动拒绝（避免误杀“全局闪光”）
            if (pts.Count < 3) return false;

            double dist = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                double dx = pts[i].Center.X - pts[i - 1].Center.X;
                double dy = pts[i].Center.Y - pts[i - 1].Center.Y;
                dist += Math.Sqrt(dx * dx + dy * dy);
            }

            // 估计画面宽度：用 bbox 的最大右边界近似（足够用于比率判定）
            int frameWidthEst = 1;
            for (int i = 0; i < pts.Count; i++)
            {
                int v = pts[i].BBox.X + pts[i].BBox.Width;
                if (v > frameWidthEst) frameWidthEst = v;
            }

            double ratio = Math.Max(0.01, algo.MaxMotionRatioPerSec);
            return dist >= frameWidthEst * ratio;
        }

        private static void EnqueueWindow(Queue<SamplePoint> q, SamplePoint sp, int windowMaxMs)
        {
            q.Enqueue(sp);
            TrimWindowByMs(q, windowMaxMs);
        }

        private static void TrimWindowByMs(Queue<SamplePoint> q, int windowMaxMs)
        {
            if (q.Count <= 0) return;

            int newest = 0;
            foreach (var x in q)
                if (x.TimeMs > newest) newest = x.TimeMs;

            while (q.Count > 0)
            {
                var head = q.Peek();
                if (newest - head.TimeMs <= windowMaxMs) break;
                q.Dequeue();
            }
        }

        private static void TrimWindowToRecent(Queue<SamplePoint> q, int keepMs)
        {
            TrimWindowByMs(q, keepMs);
        }

        private static Mat ResizeIfNeeded(Mat src, int resizeMaxWidth, out double scale)
        {
            scale = 1.0;

            if (resizeMaxWidth <= 0 || src.Width <= resizeMaxWidth)
                return src.Clone();

            scale = resizeMaxWidth * 1.0 / src.Width;
            int h = (int)Math.Round(src.Height * scale);

            var dst = new Mat();
            Cv2.Resize(src, dst, new Size(resizeMaxWidth, h), 0, 0, InterpolationFlags.Area);
            return dst;
        }

        private static double CalcRatioAboveThreshold(Mat gray, int thr)
        {
            using var mask = new Mat();
            Cv2.Threshold(gray, mask, thr, 255, ThresholdTypes.Binary);
            double count = Cv2.CountNonZero(mask);
            double total = Math.Max(1.0, gray.Width * gray.Height);
            return count / total;
        }

        private static Rect ClampRect(Rect r, int w, int h)
        {
            if (w <= 1 || h <= 1) return r;

            int x = Math.Max(0, Math.Min(w - 1, r.X));
            int y = Math.Max(0, Math.Min(h - 1, r.Y));

            int rw = Math.Max(1, Math.Min(w - x, r.Width));
            int rh = Math.Max(1, Math.Min(h - y, r.Height));

            return new Rect(x, y, rw, rh);
        }
    }
}