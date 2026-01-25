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
    /// 闪光/火花检测服务（高召回稳健基线版 + Top5强约束 + 落盘可靠性）
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

            if (job.Status == 4 || job.Status == 2 || job.Status == 3) return;
            if (vf.Status == 2 || vf.Status == 3) return;

            vf.Status = 1;
            vf.UpdTime = DateTime.Now;
            await _db.SaveChangesAsync(ct);

            var sw = Stopwatch.StartNew();

            try
            {
                var algo = AlgoParams.ParseOrDefault(job.AlgoParamsJson);

                var (eventCount, durationSec, width, height, analyzeSec) =
                    await ProcessSingleVideoAsync(job, vf, algo, ct);

                vf.DurationSec = durationSec;
                vf.Width = width;
                vf.Height = height;

                // 如果你实体没有这些字段，请删除
                vf.EventCount = eventCount;
                vf.AnalyzeSec = (int)analyzeSec;

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
                vf.AnalyzeSec = (int)sw.Elapsed.TotalSeconds;

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

            if (job.Status == 4) return;
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
                job.Progress = (int)Math.Round(finished * 100.0 / job.TotalVideoCount);
                if (finished + failed >= job.TotalVideoCount)
                {
                    job.Status = 2;
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
        // Frame ring buffer (JPEG bytes)
        // =========================
        private sealed class FrameBufferItem
        {
            public int TimeMs { get; set; }
            public int FrameIndex { get; set; }
            public byte[] Jpeg { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class FrameRingBuffer : IDisposable
        {
            private readonly int _maxItems;
            private readonly Queue<FrameBufferItem> _q = new Queue<FrameBufferItem>();

            public FrameRingBuffer(int maxItems)
            {
                _maxItems = Math.Max(3, maxItems);
            }

            public void AddJpeg(int timeMs, int frameIndex, Mat srcBgr, int jpegQuality)
            {
                if (srcBgr == null || srcBgr.Empty()) return;

                byte[] bytes;
                try
                {
                    Cv2.ImEncode(".jpg", srcBgr, out var buf,
                        new ImageEncodingParam(ImwriteFlags.JpegQuality, Math.Max(30, Math.Min(95, jpegQuality))));
                    bytes = buf?.ToArray();
                }
                catch
                {
                    bytes = null;
                }

                _q.Enqueue(new FrameBufferItem
                {
                    TimeMs = timeMs,
                    FrameIndex = frameIndex,
                    Jpeg = bytes,
                    Width = srcBgr.Width,
                    Height = srcBgr.Height
                });

                while (_q.Count > _maxItems) _q.Dequeue();
            }

            public void TrimByTime(int keepMs)
            {
                if (_q.Count == 0) return;

                int newest = 0;
                foreach (var it in _q)
                    if (it.TimeMs > newest) newest = it.TimeMs;

                while (_q.Count > 0)
                {
                    var head = _q.Peek();
                    if (newest - head.TimeMs <= keepMs) break;
                    _q.Dequeue();
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

            public void Dispose() => _q.Clear();
        }

        private sealed class SamplePoint
        {
            public int FrameIndex { get; set; }
            public int TimeMs { get; set; }

            public double Mean { get; set; }
            public double Std { get; set; }

            public double MeanDelta { get; set; }
            public double BrightRatio { get; set; }
            public double BrightDelta { get; set; }

            public double MeanRise { get; set; }
            public double BrightRise { get; set; }

            public bool Candidate { get; set; }
            public Rect BBox { get; set; }         // 原图坐标
            public double AreaRatio { get; set; }
            public double Confidence { get; set; }
            public Point2d Center { get; set; }    // 原图坐标
        }

        // =========================
        // TopK Snapshot Keeper
        // =========================
        private sealed class TopKSnapshotKeeper
        {
            public sealed class ReplaceDecision
            {
                public bool KeepNew { get; set; }
                public int? ReplaceOldSnapshotId { get; set; }
                public string ReplaceOldImagePath { get; set; }
            }

            private sealed class Item
            {
                public int SnapshotId { get; set; }
                public string ImagePath { get; set; }
                public double Confidence { get; set; }
            }

            private readonly int _k;
            private readonly List<Item> _items;

            public TopKSnapshotKeeper(int k)
            {
                _k = Math.Max(1, k);
                _items = new List<Item>(_k + 2);
            }

            public void Seed(int snapshotId, string imagePath, double conf)
            {
                if (_items.Count >= _k) return;
                _items.Add(new Item { SnapshotId = snapshotId, ImagePath = imagePath, Confidence = conf });
            }

            public ReplaceDecision Decide(double conf)
            {
                if (_items.Count < _k)
                    return new ReplaceDecision { KeepNew = true };

                int minIdx = 0;
                double min = _items[0].Confidence;
                for (int i = 1; i < _items.Count; i++)
                {
                    if (_items[i].Confidence < min)
                    {
                        min = _items[i].Confidence;
                        minIdx = i;
                    }
                }

                if (conf <= min + 1e-12)
                    return new ReplaceDecision { KeepNew = false };

                return new ReplaceDecision
                {
                    KeepNew = true,
                    ReplaceOldSnapshotId = _items[minIdx].SnapshotId,
                    ReplaceOldImagePath = _items[minIdx].ImagePath
                };
            }

            public void CommitKept(int snapshotId, string imagePath, double conf, int? replaceOldSnapshotId)
            {
                if (_items.Count < _k && replaceOldSnapshotId == null)
                {
                    _items.Add(new Item { SnapshotId = snapshotId, ImagePath = imagePath, Confidence = conf });
                    return;
                }

                if (replaceOldSnapshotId.HasValue)
                {
                    int idx = _items.FindIndex(x => x.SnapshotId == replaceOldSnapshotId.Value);
                    if (idx >= 0)
                    {
                        _items[idx] = new Item { SnapshotId = snapshotId, ImagePath = imagePath, Confidence = conf };
                        return;
                    }
                }

                int minIdx2 = 0;
                double min2 = _items[0].Confidence;
                for (int i = 1; i < _items.Count; i++)
                {
                    if (_items[i].Confidence < min2)
                    {
                        min2 = _items[i].Confidence;
                        minIdx2 = i;
                    }
                }
                _items[minIdx2] = new Item { SnapshotId = snapshotId, ImagePath = imagePath, Confidence = conf };
            }
        }

        private async Task DeleteSnapshotHardAsync(int snapshotId, string imagePath, CancellationToken ct)
        {
            try
            {
                // 1) 先找 Local（避免重复 Attach）
                var local = _db.VideoAnalysisSnapshots.Local.FirstOrDefault(x => x.Id == snapshotId);
                if (local != null)
                {
                    _db.VideoAnalysisSnapshots.Remove(local);
                }
                else
                {
                    // 2) 不 Attach stub，直接查一条再删（只多一次查询，但最稳定）
                    var entity = await _db.VideoAnalysisSnapshots
                        .FirstOrDefaultAsync(x => x.Id == snapshotId, ct);

                    if (entity != null)
                        _db.VideoAnalysisSnapshots.Remove(entity);
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeleteSnapshot DB failed. snapshotId={SnapshotId}", snapshotId);
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    File.Delete(imagePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeleteSnapshot file failed. path={Path}", imagePath);
            }
        }

        private void SafeDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SafeDeleteFile failed. path={Path}", path);
            }
        }

        private async Task SeedTop5KeeperFromDbAsync(int videoFileId, TopKSnapshotKeeper keeper, CancellationToken ct)
        {
            var top = await _db.VideoAnalysisSnapshots.AsNoTracking()
                .Where(s => s.VideoFileId == videoFileId)
                .OrderByDescending(s => s.Confidence)
                .ThenByDescending(s => s.Id)
                .Take(5)
                .Select(s => new { s.Id, s.ImagePath, s.Confidence })
                .ToListAsync(ct);

            foreach (var x in top)
                keeper.Seed(x.Id, x.ImagePath, (double)x.Confidence);
        }

        private async Task EnforceTop5HardAsync(int videoFileId, int k, CancellationToken ct)
        {
            var all = await _db.VideoAnalysisSnapshots.AsNoTracking()
                .Where(s => s.VideoFileId == videoFileId)
                .OrderByDescending(s => s.Confidence)
                .ThenByDescending(s => s.Id)
                .Select(s => new { s.Id, s.ImagePath })
                .ToListAsync(ct);

            if (all.Count <= k) return;

            foreach (var d in all.Skip(k))
            {
                await DeleteSnapshotHardAsync(d.Id, d.ImagePath, ct);
            }
        }

        // =========================
        // Robust Baseline
        // =========================
        private sealed class RobustBaseline
        {
            public double MeanEwma { get; private set; }
            public double BrightEwma { get; private set; }
            public bool HasInit { get; private set; }

            private readonly Queue<(double mean, double br)> _hist = new Queue<(double, double)>();
            private readonly int _maxHist;

            public RobustBaseline(int maxHist = 60)
            {
                _maxHist = Math.Max(10, maxHist);
            }

            public void Reset()
            {
                HasInit = false;
                MeanEwma = 0;
                BrightEwma = 0;
                _hist.Clear();
            }

            public void Update(double mean, double br, double alpha, bool allowUpdate)
            {
                if (!allowUpdate) return;

                if (!HasInit)
                {
                    MeanEwma = mean;
                    BrightEwma = br;
                    HasInit = true;
                }
                else
                {
                    MeanEwma = MeanEwma * (1.0 - alpha) + mean * alpha;
                    BrightEwma = BrightEwma * (1.0 - alpha) + br * alpha;
                }

                _hist.Enqueue((mean, br));
                while (_hist.Count > _maxHist) _hist.Dequeue();
            }

            public (double meanBase, double brBase) GetRobustBase()
            {
                if (!HasInit) return (0, 0);
                if (_hist.Count < 8) return (MeanEwma, BrightEwma);

                var arrM = _hist.Select(x => x.mean).OrderBy(x => x).ToArray();
                var arrB = _hist.Select(x => x.br).OrderBy(x => x).ToArray();

                double medM = arrM[arrM.Length / 2];
                double medB = arrB[arrB.Length / 2];

                double baseM = 0.6 * MeanEwma + 0.4 * medM;
                double baseB = 0.6 * BrightEwma + 0.4 * medB;

                return (baseM, baseB);
            }
        }

        private async Task<(int eventCount, int? durationSec, int? width, int? height, double analyzeSec)> ProcessSingleVideoAsync(
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

            int sampleEveryFrames = CalcSampleEveryFrames(fps, algo, _logger);

            int w0 = (int)cap.FrameWidth;
            int h0 = (int)cap.FrameHeight;

            int consecutiveHits = 0;
            int lastEventFrame = -999999;

            bool pendingPulse = false;
            int pendingStartMs = 0;
            int pendingLastHitMs = 0;

            var window = new Queue<SamplePoint>();
            int windowMaxMs = 4500;
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
                return (0, null, w0, h0, sw.Elapsed.TotalSeconds);

            int effSampleFps = Math.Max(1, (int)Math.Round(fps * 1.0 / sampleEveryFrames));
            using var frameBuf = new FrameRingBuffer(maxItems: Math.Max(24, effSampleFps * 5));

            var top5Keeper = new TopKSnapshotKeeper(5);
            await SeedTop5KeeperFromDbAsync(vf.Id, top5Keeper, ct);

            var baseline = new RobustBaseline(maxHist: Math.Max(30, effSampleFps * 6));
            double alpha = Math.Max(0.02, Math.Min(0.12, effSampleFps / 100.0));

            int foundEvents = 0;
            var deletedSnapshotIds = new HashSet<int>();

            while (cap.Read(curr))
            {
                ct.ThrowIfCancellationRequested();

                frameCounter++;
                if (curr.Empty()) break;

                if (frameCounter % sampleEveryFrames != 0)
                    continue;

                int timeMs = (int)Math.Round(frameCounter * 1000.0 / fps);

                frameBuf.AddJpeg(timeMs, frameCounter, curr, jpegQuality: 85);
                frameBuf.TrimByTime(keepMs: 5200);

                var stats = BuildStatsPoint(prev, curr, algo);
                stats.FrameIndex = frameCounter;
                stats.TimeMs = timeMs;

                var (baseMean, baseBR) = baseline.GetRobustBase();
                stats.MeanRise = stats.Mean - baseMean;
                stats.BrightRise = stats.BrightRatio - baseBR;

                bool isCandidate = TryDetectCandidate(prev, curr, algo, stats, out var cand);
                if (isCandidate && cand != null)
                {
                    stats.Candidate = true;
                    stats.BBox = cand.BBox;
                    stats.AreaRatio = cand.AreaRatio;
                    stats.Confidence = cand.Confidence;
                    stats.Center = cand.Center;

                    stats.MeanRise = cand.MeanRise;
                    stats.BrightRise = cand.BrightRise;
                }
                else
                {
                    stats.Candidate = isCandidate;
                }

                EnqueueWindow(window, stats, windowMaxMs);

                bool allowBaselineUpdate = (!pendingPulse && !isCandidate);
                baseline.Update(stats.Mean, stats.BrightRatio, alpha, allowBaselineUpdate);

                if (isCandidate)
                {
                    consecutiveHits++;

                    int needHits = Math.Max(1, algo.RequireConsecutiveHits);
                    if (stats.MeanRise >= algo.MeanDeltaRise * 0.9 || stats.BrightRise >= algo.BrightRatioDelta * 0.9)
                        needHits = 1;

                    if (consecutiveHits >= needHits)
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
                    consecutiveHits = Math.Max(0, consecutiveHits - 1);
                }

                if (pendingPulse && timeMs - pendingLastHitMs > Math.Max(1500, pulseMaxMs + 800))
                {
                    pendingPulse = false;
                    consecutiveHits = 0;
                    TrimWindowToRecent(window, keepMs: 2000);
                }

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
                            var peakFrame = frameBuf.GetNearest(peakTimeMs);
                            int snapTimeMs = peakFrame?.TimeMs ?? peakTimeMs;
                            int snapFrameIndex = peakFrame?.FrameIndex ?? frameCounter;

                            int timeSec = (int)Math.Round(snapTimeMs / 1000.0);
                            byte eventType = (byte)(isFlash ? 1 : 2);

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

                            // ====== Top5 截图（强约束 + 落盘可靠）======
                            snapshotSeq++;

                            var decision = top5Keeper.Decide(conf);
                            if (decision.KeepNew)
                            {
                                string fileName = $"{vf.Id}_{snapFrameIndex}_t{timeSec}s_conf{conf:0.000}_{Guid.NewGuid():N}.jpg";
                                string imagePath = Path.Combine(snapDir, fileName);

                                int imgW = curr.Width;
                                int imgH = curr.Height;

                                try
                                {
                                    if (peakFrame?.Jpeg != null && peakFrame.Jpeg.Length > 0)
                                    {
                                        await File.WriteAllBytesAsync(imagePath, peakFrame.Jpeg, ct);
                                        imgW = peakFrame.Width;
                                        imgH = peakFrame.Height;
                                    }
                                    else
                                    {
                                        Cv2.ImWrite(imagePath, curr);
                                        imgW = curr.Width;
                                        imgH = curr.Height;
                                    }

                                    var fi = new FileInfo(imagePath);
                                    if (!fi.Exists || fi.Length <= 0)
                                    {
                                        _logger.LogWarning("Snapshot file missing/empty after write. path={Path}", imagePath);
                                        SafeDeleteFile(imagePath);
                                    }
                                    else
                                    {
                                        var snap = new VideoAnalysisSnapshot
                                        {
                                            VideoFileId = vf.Id,     // ★关键：写入新增字段
                                            EventId = openEvent.Id,
                                            ImagePath = imagePath,
                                            TimeSec = timeSec,
                                            FrameIndex = snapFrameIndex,
                                            ImageWidth = imgW,
                                            ImageHeight = imgH,
                                            SeqNo = snapshotSeq,
                                            Confidence = (decimal)conf,
                                            CrtTime = DateTime.Now,
                                            UpdTime = DateTime.Now
                                        };

                                        _db.VideoAnalysisSnapshots.Add(snap);
                                        await _db.SaveChangesAsync(ct);

                                        top5Keeper.CommitKept(snap.Id, imagePath, conf, decision.ReplaceOldSnapshotId);
                                        if (decision.ReplaceOldSnapshotId.HasValue)
                                        {
                                            var oldId = decision.ReplaceOldSnapshotId.Value;

                                            if (deletedSnapshotIds.Add(oldId))
                                            {
                                                if (!string.Equals(decision.ReplaceOldImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    await DeleteSnapshotHardAsync(oldId, decision.ReplaceOldImagePath, ct);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Snapshot write failed. path={Path}", imagePath);
                                    SafeDeleteFile(imagePath);
                                }
                            }

                            lastEventFrame = snapFrameIndex;
                        }
                    }

                    pendingPulse = false;
                    consecutiveHits = 0;
                    TrimWindowToRecent(window, keepMs: 2200);
                }

                curr.CopyTo(prev);
            }

            // ★最终硬裁剪：保证最多5张
            await EnforceTop5HardAsync(vf.Id, 5, ct);

            int? durationSec = cap.FrameCount > 0 && cap.Fps > 0
                ? (int?)Math.Round(cap.FrameCount / cap.Fps)
                : null;

            sw.Stop();
            return (foundEvents, durationSec, w0, h0, sw.Elapsed.TotalSeconds);
        }

        private static int CalcSampleEveryFrames(int fps, AlgoParams algo, ILogger logger)
        {
            int targetFps = algo.SampleFps;

            if (targetFps <= 0)
            {
                targetFps = 8;
                logger?.LogWarning("AlgoParams.SampleFps <= 0, fallback to default 8fps. Please set SampleFps explicitly.");
            }

            if (targetFps > fps) targetFps = fps;

            return Math.Max(1, (int)Math.Round(fps * 1.0 / targetFps));
        }

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

                MeanRise = 0,
                BrightRise = 0,

                Candidate = false,
                BBox = default,
                AreaRatio = 0,
                Confidence = 0,
                Center = default
            };
        }

        private static bool TryDetectCandidate(Mat prev, Mat curr, AlgoParams algo, SamplePoint stats, out SamplePoint cand)
        {
            cand = null;

            bool riseHit =
                stats.MeanRise >= algo.MeanDeltaRise ||
                stats.BrightRise >= algo.BrightRatioDelta;

            bool deltaHit =
                stats.MeanDelta >= algo.GlobalBrightnessDelta ||
                stats.BrightDelta >= algo.BrightRatioDelta;

            if (riseHit || deltaHit)
                return true;

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
            Cv2.MorphologyEx(diff, diff, MorphTypes.Close, kernel);

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
                Math.Min(1.0, areaRatio * 7.0) * 0.45 +
                Math.Min(1.0, stats.BrightDelta / 0.006) * 0.25 +
                Math.Min(1.0, stats.MeanDelta / 18.0) * 0.20 +
                Math.Min(1.0, Math.Max(0, stats.MeanRise) / 20.0) * 0.10;

            cand = new SamplePoint
            {
                Mean = stats.Mean,
                Std = stats.Std,
                MeanDelta = stats.MeanDelta,
                BrightRatio = stats.BrightRatio,
                BrightDelta = stats.BrightDelta,
                MeanRise = stats.MeanRise,
                BrightRise = stats.BrightRise,

                BBox = bbox,
                AreaRatio = areaRatio,
                Confidence = Math.Max(0.08, Math.Min(1.0, score)),
                Center = center,
                Candidate = true
            };
            return true;
        }

        /// <summary>
        /// 短脉冲确认（召回优先稳健基线版）
        /// - rise：峰值相对基线抬升（MeanRise/BrightRise）达标，或窗口内出现一次强 delta
        /// - fall：峰值后 (pulseMaxMs + 400ms) 内回落到 baseline 附近
        /// - 持续短：高亮跨度不超过 MaxPulseSec
        /// 抑制：
        /// - 持续高亮 >= SustainRejectSec
        /// - 移动光源（bbox 中心位移过大，仅对 bbox 足够的候选生效）
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

            if (window == null || window.Count < 3)
                return false;

            var arr = window.ToArray();
            Array.Sort(arr, (a, b) => a.TimeMs.CompareTo(b.TimeMs));
            int n = arr.Length;

            // 选峰值：优先局部 bbox/conf，否则用 MeanRise 或 MeanDelta
            int peakIdx = SelectPeakIndex(arr, algo, out bool peakIsLocal);
            var peak = arr[peakIdx];
            peakTimeMs = peak.TimeMs;

            bestBox = GetBestBoxNearPeak(arr, peakTimeMs);

            // -------- baseline：使用“峰值前 800ms~1500ms”范围内的稳健中位数（避免被事件污染）--------
            int baseEnd = peakTimeMs - 200;
            int baseStart = Math.Max(arr[0].TimeMs, peakTimeMs - 1500);

            var pre = arr.Where(p => p.TimeMs >= baseStart && p.TimeMs <= baseEnd).ToArray();
            if (pre.Length < 2)
            {
                // 兜底：用峰值前所有点
                pre = arr.Where(p => p.TimeMs <= baseEnd).ToArray();
            }
            if (pre.Length < 2)
            {
                // 再兜底：用最早两点
                pre = arr.Take(Math.Min(2, n)).ToArray();
            }

            double baseMean = Median(pre.Select(x => x.Mean));
            double baseBR = Median(pre.Select(x => x.BrightRatio));

            // -------- rise：相对基线抬升优先，其次相邻帧突变 --------
            double peakMeanRise = peak.Mean - baseMean;
            double peakBrRise = peak.BrightRatio - baseBR;

            bool hasRise =
                peakMeanRise >= algo.MeanDeltaRise ||
                peakBrRise >= algo.BrightRatioDelta;

            if (!hasRise)
            {
                // 兜底：窗口内任何点出现强 delta 也算 rise
                for (int i = 0; i < n; i++)
                {
                    if (arr[i].MeanDelta >= algo.GlobalBrightnessDelta ||
                        arr[i].BrightDelta >= algo.BrightRatioDelta)
                    {
                        hasRise = true;
                        break;
                    }
                }
            }
            if (!hasRise) return false;

            // -------- fall：允许 pulseMaxMs + 400ms（采样稀疏时常见漏点）--------
            double meanBackTo = baseMean + algo.MeanDeltaFall;
            double brBackTo = baseBR + Math.Max(0.0005, algo.BrightRatioDelta * 0.8);

            bool hasFall = false;
            int fallEnd = peakTimeMs + pulseMaxMs + 400;
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

            // -------- 高亮持续时间（用 mean 超过 baseMean+MeanDeltaRise 的跨度）--------
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

            sustainReject = (highSpanMs >= (int)(algo.SustainRejectSec * 1000));
            motionReject = CheckMotionReject(arr, peakTimeMs, algo);

            int maxPulseMs = (int)(algo.MaxPulseSec * 1000);
            if (highSpanMs > Math.Max(260, maxPulseMs)) return false;

            // -------- isFlash 判定：全局 meanRise 更可靠；局部用 areaRatio 辅助 --------
            bool flashByMean = peakMeanRise >= Math.Max(algo.MeanDeltaRise, algo.GlobalBrightnessDelta * 0.75);
            bool flashByArea = peak.AreaRatio >= algo.FlashAreaRatio;

            // 召回优先：peakIsLocal 时也允许 flashByMean 触发 flash（避免大范围闪光但 bbox 只抓到局部）
            isFlash = (flashByMean || flashByArea);

            // -------- confidence：更偏向“峰值相对基线抬升”--------
            if (peakIsLocal)
            {
                double score =
                    Math.Min(1.0, Math.Max(0, peakBrRise) / 0.006) * 0.35 +
                    Math.Min(1.0, peak.AreaRatio * 7.0) * 0.25 +
                    Math.Min(1.0, Math.Max(0, peakMeanRise) / 22.0) * 0.30 +
                    (hasFall ? 0.10 : 0.0);

                confidence = Math.Max(0.10, Math.Min(1.0, score));
            }
            else
            {
                double score =
                    Math.Min(1.0, Math.Max(0, peakMeanRise) / 22.0) * 0.60 +
                    Math.Min(1.0, peak.MeanDelta / 18.0) * 0.15 +
                    Math.Min(1.0, Math.Max(0, peakBrRise) / 0.006) * 0.15 +
                    (hasFall ? 0.10 : 0.0);

                confidence = Math.Max(0.10, Math.Min(1.0, score));
            }

            return true;
        }
        private static double Median(IEnumerable<double> values)
        {
            if (values == null) return 0;

            var arr = values as double[] ?? values.ToArray();
            if (arr.Length == 0) return 0;

            Array.Sort(arr);

            int mid = arr.Length / 2;
            if ((arr.Length % 2) == 1)
                return arr[mid];

            // 偶数个：取中间两数平均
            return (arr[mid - 1] + arr[mid]) / 2.0;
        }

        private static int SelectPeakIndex(SamplePoint[] arr, AlgoParams algo, out bool peakIsLocal)
        {
            peakIsLocal = false;
            int n = arr.Length;

            // 1) 局部候选：优先 bbox 点里选 (Confidence 或 BrightRise/BrightDelta)
            int bestIdx = -1;
            double bestScore = double.MinValue;

            for (int i = 0; i < n; i++)
            {
                bool hasBox = arr[i].BBox.Width > 0 && arr[i].BBox.Height > 0;
                if (!hasBox) continue;

                double s = arr[i].Confidence;
                s = Math.Max(s, arr[i].BrightDelta * 1000.0);
                s = Math.Max(s, Math.Max(0, arr[i].BrightRise) * 800.0);

                if (s > bestScore)
                {
                    bestScore = s;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                peakIsLocal = true;
                return bestIdx;
            }

            // 2) 全局：优先 MeanRise 最大，其次 MeanDelta 最大
            int peakIdx = 0;
            double maxRise = arr[0].MeanRise;
            for (int i = 1; i < n; i++)
            {
                if (arr[i].MeanRise > maxRise)
                {
                    maxRise = arr[i].MeanRise;
                    peakIdx = i;
                }
            }

            if (maxRise > 0.1)
            {
                peakIsLocal = false;
                return peakIdx;
            }

            int peakIdx2 = 0;
            double maxMd = arr[0].MeanDelta;
            for (int i = 1; i < n; i++)
            {
                if (arr[i].MeanDelta > maxMd)
                {
                    maxMd = arr[i].MeanDelta;
                    peakIdx2 = i;
                }
            }

            peakIsLocal = false;
            return peakIdx2;
        }

        private static Rect GetBestBoxNearPeak(SamplePoint[] arr, int peakTimeMs)
        {
            int bestJ = -1;
            int bestAbs = int.MaxValue;

            for (int j = 0; j < arr.Length; j++)
            {
                if (arr[j].BBox.Width <= 0 || arr[j].BBox.Height <= 0) continue;
                int d = Math.Abs(arr[j].TimeMs - peakTimeMs);
                if (d < bestAbs)
                {
                    bestAbs = d;
                    bestJ = j;
                }
            }

            return bestJ >= 0 ? arr[bestJ].BBox : default;
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
