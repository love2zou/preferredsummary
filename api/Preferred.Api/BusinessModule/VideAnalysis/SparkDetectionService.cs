using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// 闪光/火花检测服务（高召回稳健基线版 + 事件Top5强约束 + 每事件唯一截图）
    /// 主流程：
    /// 1. 根据 fileId 读取视频文件和所属任务，判断任务/文件当前状态是否允许分析。
    /// 2. 逐帧采样视频，计算灰度均值、亮点比例、帧间差异等统计量。
    /// 3. 用稳健基线过滤环境亮度缓慢变化，只把短时间突增的亮度变化作为候选事件。
    /// 4. 通过短脉冲确认、持续高亮抑制、移动光源抑制，降低常亮灯、移动光源的误报。
    /// 5. 将事件、截图和聚合进度写回数据库，并强制每个视频最多保留 5 个高置信事件，且每个事件固定绑定 1 张截图。
    /// </summary>
    public sealed class SparkDetectionService
    {
                /// <summary>
        /// EF Core 数据库上下文。
        /// 负责读写任务、视频文件、检测事件、截图等业务表。
        /// </summary>
        private readonly ApplicationDbContext _db;
                /// <summary>
        /// 视频及截图的根存储目录。
        /// 从 FileStorageConfig.VideoRootPath 注入，后续会在该目录下按任务号创建子目录。
        /// </summary>
        private readonly string _root;
                /// <summary>
        /// 日志对象。
        /// 用于记录分析过程中的错误、警告以及关键运行信息，便于排查问题。
        /// </summary>
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
                /// <summary>
        /// 单个视频最终最多保留的事件数。
        /// 处理完成后会按事件置信度从高到低排序，仅保留前 MaxKeepEvents 个事件。
        /// 每个保留事件只绑定 1 张代表截图。
        /// </summary>
        private const int MaxKeepEvents = 6; // 最多 6 个事件，每个事件固定绑定 1 张截图
        /// <summary>
        /// 处理单个视频文件。
        /// 该方法是 Worker 调用检测服务的入口：负责更新文件状态、调用核心视频分析逻辑、
        /// 成功后写入视频元数据，失败时记录错误并刷新任务聚合信息。
        /// </summary>
        public async Task ProcessFileAsync(int fileId)
        {
            var vf = await _db.VideoAnalysisFiles.FirstOrDefaultAsync(x => x.Id == fileId);
            if (vf == null) return;

            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.Id == vf.JobId);
            if (job == null) return;

            if (job.Status == 4 || job.Status == 2 || job.Status == 3) return;
            // 成功文件不重复分析；失败文件允许被 Worker 重试队列重新投递后再次分析。
            if (vf.Status == 2) return;

            vf.Status = 1;
            vf.UpdTime = DateTime.Now;
            await _db.SaveChangesAsync();

            var sw = Stopwatch.StartNew();

            try
            {
                var algo = AlgoParams.ParseOrDefault(job.AlgoParamsJson);

                var (eventCount, durationSec, width, height, analyzeSec) =
                    await ProcessSingleVideoAsync(job, vf, algo);

                vf.DurationSec = durationSec;
                vf.Width = width;
                vf.Height = height;

                // 如果你实体没有这些字段，请删除
                vf.EventCount = eventCount;
                vf.AnalyzeSec = (int)analyzeSec;

                vf.Status = 2;
                vf.ErrorMessage = null;
                vf.UpdTime = DateTime.Now;

                await _db.SaveChangesAsync();
                await UpdateJobAggregateAsync(job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessFile failed. fileId={FileId}", fileId);

                vf.Status = 3;
                vf.ErrorMessage = ex.Message;

                // 如果你实体没有该字段，请删除
                vf.AnalyzeSec = (int)sw.Elapsed.TotalSeconds;

                vf.UpdTime = DateTime.Now;
                await _db.SaveChangesAsync();

                await UpdateJobAggregateAsync(vf.JobId);
                throw;
            }
        }

        /// <summary>
        /// 汇总任务进度。
        /// 每次文件成功或失败后重新统计同一任务下的文件状态和事件数量，保证列表页/详情页展示一致。
        /// </summary>
        private async Task UpdateJobAggregateAsync(int jobId)
        {
            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId);
            if (job == null) return;

            if (job.Status == 4) return;
            if (job.Status == 2 || job.Status == 3) return;

            var files = await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.JobId == jobId)
                .Select(x => new { x.Status })
                .ToListAsync();

            int totalUploaded = files.Count;
            int finished = files.Count(x => x.Status == 2);
            int failed = files.Count(x => x.Status == 3);

            int totalEvents = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == jobId)
                .CountAsync();

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

            await _db.SaveChangesAsync();
        }

        // =========================
        // Frame ring buffer (JPEG bytes)
        // 保存最近几秒的采样帧 JPEG。事件确认通常要等“亮起 -> 回落”都发生后才能判断，
        // 因此真正落盘截图时，需要回头找最接近峰值时刻的帧，而不是使用当前已经滞后的 curr。
        // =========================
        private sealed class FrameBufferItem
        {
            /// <summary>采样点对应的视频时间（毫秒）。</summary>
            public int TimeMs { get; set; }
            /// <summary>采样点对应的视频帧索引。</summary>
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

        /// <summary>
        /// 单个采样点的统计结果对象。
        ///
        /// 注意：
        /// - 它不是最终事件；
        /// - 它只是视频某个采样时刻的亮度统计快照；
        /// - 后续候选检测、窗口确认、峰值选择都基于这一结构进行。
        /// </summary>
        private sealed class SamplePoint
        {
            /// <summary>采样点对应的视频帧索引。</summary>
            public int FrameIndex { get; set; }
            /// <summary>采样点对应的视频时间（毫秒）。</summary>
            public int TimeMs { get; set; }

            /// <summary>当前帧灰度均值，反映整体亮度水平。</summary>
            public double Mean { get; set; }
            /// <summary>当前帧灰度标准差，反映亮度分布离散程度。</summary>
            public double Std { get; set; }

            /// <summary>当前帧相对于前一采样帧的亮度变化量。</summary>
            public double MeanDelta { get; set; }
            /// <summary>高亮像素占比。</summary>
            public double BrightRatio { get; set; }
            /// <summary>高亮像素占比相对前一采样帧的变化量。</summary>
            public double BrightDelta { get; set; }

            /// <summary>相对于稳健基线的亮度抬升量。</summary>
            public double MeanRise { get; set; }
            /// <summary>相对于稳健基线的高亮占比抬升量。</summary>
            public double BrightRise { get; set; }

            /// <summary>当前采样点是否被标记为候选点。</summary>
            public bool Candidate { get; set; }
            /// <summary>候选目标框，使用原图坐标系。</summary>
            public Rect BBox { get; set; }         // 原图坐标
            /// <summary>候选区域面积占整帧面积的比例。</summary>
            public double AreaRatio { get; set; }
            /// <summary>候选点或事件置信度。</summary>
            public double Confidence { get; set; }
            /// <summary>候选目标中心点，使用原图坐标系。</summary>
            public Point2d Center { get; set; }    // 原图坐标
        }

                /// <summary>
        /// 物理删除指定事件及其关联截图。
        ///
        /// 使用场景：
        /// 1. 单个视频检测完成后，只保留前 N 个高置信事件；
        /// 2. 被淘汰的事件需要连同截图记录和磁盘文件一并删除，避免数据库和文件系统残留脏数据。
        ///
        /// 删除顺序：
        /// - 先删截图文件与 VideoAnalysisSnapshots 记录；
        /// - 再删 VideoAnalysisEvents 事件记录。
        /// 这样做是为了保证事件被删后，不会留下悬空截图记录。
        /// </summary>
        private async Task DeleteEventWithSnapshotsHardAsync(int eventId)
        {
            try
            {
                var snaps = await _db.VideoAnalysisSnapshots
                    .Where(x => x.EventId == eventId)
                    .ToListAsync();

                foreach (var snap in snaps)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(snap.ImagePath) && File.Exists(snap.ImagePath))
                            File.Delete(snap.ImagePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Delete snapshot file failed. eventId={EventId}, path={Path}", eventId, snap.ImagePath);
                    }
                }

                if (snaps.Count > 0)
                {
                    _db.VideoAnalysisSnapshots.RemoveRange(snaps);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Delete snapshots by event failed. eventId={EventId}", eventId);
            }

            try
            {
                var entity = await _db.VideoAnalysisEvents.FirstOrDefaultAsync(x => x.Id == eventId);
                if (entity != null)
                {
                    _db.VideoAnalysisEvents.Remove(entity);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Delete event failed. eventId={EventId}", eventId);
            }
        }

                /// <summary>
        /// 安全删除文件。
        ///
        /// 删除失败时只记日志，不向上抛异常，避免“清理失败”影响主流程。
        /// 常用于截图替换后的旧文件删除、异常回滚删除等辅助场景。
        /// </summary>
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

                /// <summary>
        /// 为指定事件插入或更新唯一截图。
        ///
        /// 当前版本的策略是“每个事件只保留 1 张截图”：
        /// - 如果该事件还没有截图，则新建一条截图记录；
        /// - 如果该事件已经有截图，则用当前更优的截图覆盖旧截图，并删除旧文件。
        ///
        /// 截图来源优先级：
        /// 1. 优先使用环形帧缓存中与峰值时间最接近的 JPEG；
        /// 2. 如果缓存帧不存在，则退化为当前帧 curr。
        ///
        /// 这样可以尽量保证截图更接近真正峰值时刻，而不是事件确认时的滞后帧。
        /// </summary>
        private async Task UpsertEventSnapshotAsync(
            VideoAnalysisFile vf,
            int eventId,
            int timeSec,
            int snapFrameIndex,
            double conf,
            FrameBufferItem peakFrame,
            Mat curr,
            string snapDir,
            int snapshotSeq)
        {
            if (eventId <= 0) return;

            var oldSnap = await _db.VideoAnalysisSnapshots
                .FirstOrDefaultAsync(x => x.EventId == eventId);

            string fileName = $"{vf.Id}_{snapFrameIndex}_e{eventId}_t{timeSec}s_conf{conf:0.000}_{Guid.NewGuid():N}.jpg";
            string imagePath = Path.Combine(snapDir, fileName);

            int imgW = curr.Width;
            int imgH = curr.Height;

            try
            {
                if (peakFrame?.Jpeg != null && peakFrame.Jpeg.Length > 0)
                {
                    await File.WriteAllBytesAsync(imagePath, peakFrame.Jpeg);
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
                    return;
                }

                if (oldSnap == null)
                {
                    var snap = new VideoAnalysisSnapshot
                    {
                        VideoFileId = vf.Id,
                        EventId = eventId,
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
                    await _db.SaveChangesAsync();
                }
                else
                {
                    string oldPath = oldSnap.ImagePath;

                    oldSnap.ImagePath = imagePath;
                    oldSnap.TimeSec = timeSec;
                    oldSnap.FrameIndex = snapFrameIndex;
                    oldSnap.ImageWidth = imgW;
                    oldSnap.ImageHeight = imgH;
                    oldSnap.Confidence = (decimal)conf;
                    oldSnap.SeqNo = snapshotSeq;
                    oldSnap.UpdTime = DateTime.Now;

                    _db.VideoAnalysisSnapshots.Update(oldSnap);
                    await _db.SaveChangesAsync();

                    if (!string.IsNullOrWhiteSpace(oldPath) &&
                        !string.Equals(oldPath, imagePath, StringComparison.OrdinalIgnoreCase))
                    {
                        SafeDeleteFile(oldPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Upsert event snapshot failed. eventId={EventId}, path={Path}", eventId, imagePath);
                SafeDeleteFile(imagePath);
            }
        }

                /// <summary>
        /// 对单个视频执行“事件 TopN 硬裁剪”。
        ///
        /// 按事件置信度从高到低排序，只保留前 k 个事件；
        /// 排在后面的事件会被连同截图一起物理删除。
        ///
        /// 说明：
        /// - 这里裁剪的是“事件”，不是“截图”；
        /// - 因为当前业务目标是“最多保留 N 个事件，且每个事件一张截图”。
        /// </summary>
        private async Task<int> EnforceTop6EventsHardAsync(int videoFileId, int k)
        {
            var allEvents = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.VideoFileId == videoFileId)
                .OrderByDescending(e => e.Confidence)
                .ThenByDescending(e => e.Id)
                .Select(e => new { e.Id })
                .ToListAsync();

            if (allEvents.Count <= k)
                return allEvents.Count;

            foreach (var e in allEvents.Skip(k))
            {
                await DeleteEventWithSnapshotsHardAsync(e.Id);
            }

            return k;
        }

        // =========================
        // Robust Baseline
        // 用 EWMA + 中位数历史构造“稳健基线”。环境光缓慢变化会更新基线，候选事件期间暂停更新，
        // 防止一次闪光/火花把基线抬高，导致后续确认阶段看不到明显 rise/fall。
        // =========================
        /// <summary>
        /// 稳健亮度基线。
        ///
        /// 这里不是简单记录最近一帧亮度，而是结合：
        /// - EWMA（指数滑动平均）
        /// - 历史样本中位数
        /// 来构造一个对异常亮点不敏感的环境基线。
        ///
        /// 目的：
        /// 防止一次瞬时闪光把“正常亮度”抬高，从而影响后续 rise/fall 判断。
        /// </summary>
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

        /// <summary>
        /// 核心视频分析流程。
        /// 这里不再接收取消标记参数，分析一旦开始就按完整视频跑完，避免 OpenCV 读帧、
        /// 截图落盘、数据库写入在中途被取消后留下半截状态。
        /// </summary>
                /// <summary>
        /// 单个视频的核心分析流程。
        ///
        /// 整体步骤：
        /// 1. 打开视频并读取 FPS、分辨率等基础信息；
        /// 2. 按目标采样率跳帧采样，降低计算量；
        /// 3. 对每个采样点计算亮度均值、亮点比例、帧差等统计特征；
        /// 4. 结合稳健基线识别“亮度突增”的候选点；
        /// 5. 使用时间窗口确认短脉冲，并过滤持续高亮、移动光源等误报；
        /// 6. 将确认结果生成事件，或合并到当前 openEvent；
        /// 7. 为每个事件维护唯一截图；
        /// 8. 全部处理结束后，只保留前 MaxKeepEvents 个高置信事件。
        ///
        /// 这里不接收取消令牌，目的是避免 OpenCV 读帧、截图落盘、数据库写入进行到一半时被中断，
        /// 导致文件状态、事件记录、截图文件之间出现不一致。
        /// </summary>
        private async Task<(int eventCount, int? durationSec, int? width, int? height, double analyzeSec)> ProcessSingleVideoAsync(
            VideoAnalysisJob job,
            VideoAnalysisFile vf,
            AlgoParams algo)
        {
            var sw = Stopwatch.StartNew();

            using var cap = new VideoCapture(vf.FilePath);
            if (!cap.IsOpened())
                throw new InvalidOperationException("VideoCapture open failed: " + vf.FilePath);

            int fps = (int)Math.Round(cap.Fps);
            if (fps <= 0) fps = 25;

            // 按配置把原始 FPS 降采样到目标采样率，减少 OpenCV 计算量。
            int sampleEveryFrames = CalcSampleEveryFrames(fps, algo, _logger);

            int w0 = (int)cap.FrameWidth;
            int h0 = (int)cap.FrameHeight;

            int consecutiveHits = 0;
            int lastEventFrame = -999999;

            bool pendingPulse = false;
            int pendingStartMs = 0;
            int pendingLastHitMs = 0;

            var window = new Queue<SamplePoint>();
            // window 保存最近几秒的统计点，确认短脉冲时需要同时看到“抬升”和“回落”。
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


            var baseline = new RobustBaseline(maxHist: Math.Max(30, effSampleFps * 6));
            double alpha = Math.Max(0.02, Math.Min(0.12, effSampleFps / 100.0));

            int foundEvents = 0;

            // =========================
            // 主采样循环
            // 说明：
            // 1. 按 sampleEveryFrames 跳帧采样，而不是逐帧全量分析；
            // 2. 每次采样都计算统计特征、候选结果并推进时间窗口；
            // 3. 一旦满足短脉冲确认条件，就尝试生成或合并事件；
            // 4. 当前版本每个事件只维护 1 张截图。
            // =========================
            while (cap.Read(curr))
            {
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

                // 候选事件期间暂停更新基线，防止闪光本身污染环境亮度基准。
                bool allowBaselineUpdate = (!pendingPulse && !isCandidate);
                baseline.Update(stats.Mean, stats.BrightRatio, alpha, allowBaselineUpdate);

                if (isCandidate)
                {
                    consecutiveHits++;

                    int needHits = Math.Max(1, algo.RequireConsecutiveHits);
                    // 如果相对基线的抬升已经很明显，就降低连续命中要求，优先保证短暂火花不漏检。
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
                    // 长时间没有新的候选命中，说明这次 pending 没有形成完整短脉冲，收缩窗口重新等待。
                    pendingPulse = false;
                    consecutiveHits = 0;
                    TrimWindowToRecent(window, keepMs: 2000);
                }

                if (pendingPulse && (timeMs - pendingStartMs >= pulseMaxMs))
                {
                    // 到达最大脉冲观察时间后再确认，确保能看到峰值之后是否回落。
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

                            // merged 表示当前确认出的短脉冲是否已并入现有 openEvent。
                            bool merged = false;

                            // needRefreshSnapshot 表示是否需要为该事件写入/更新唯一截图：
                            // - 新建事件时为 true；
                            // - 合并事件且本次置信度更高时为 true；
                            // - 仅扩展结束时间但置信度未提升时为 false。
                            bool needRefreshSnapshot = false;

                            // 优先尝试与当前 openEvent 合并，避免同一短时间段连续命中被拆成多条碎片事件。
                            if (openEvent != null &&
                                openEvent.VideoFileId == vf.Id &&
                                openEvent.EventType == eventType &&
                                (timeSec - openEvent.EndTimeSec) <= Math.Max(0, algo.MergeGapSec))
                            {
                                bool confidenceImproved = conf > (double)openEvent.Confidence;

                                openEvent.EndTimeSec = timeSec;

                                if (confidenceImproved)
                                {
                                    openEvent.PeakTimeSec = timeSec;
                                    openEvent.FrameIndex = snapFrameIndex;
                                    openEvent.Confidence = (decimal)conf;
                                    openEvent.BBoxJson = JsonConvert.SerializeObject(new
                                    {
                                        x = bestBox.X,
                                        y = bestBox.Y,
                                        w = bestBox.Width,
                                        h = bestBox.Height
                                    });

                                    needRefreshSnapshot = true;
                                }

                                openEvent.UpdTime = DateTime.Now;

                                _db.VideoAnalysisEvents.Update(openEvent);
                                await _db.SaveChangesAsync();
                                merged = true;
                            }

                            // 不能合并时，创建一个新事件。
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
                                    BBoxJson = JsonConvert.SerializeObject(new
                                    {
                                        x = bestBox.X,
                                        y = bestBox.Y,
                                        w = bestBox.Width,
                                        h = bestBox.Height
                                    }),
                                    SeqNo = eventSeq,
                                    CrtTime = DateTime.Now,
                                    UpdTime = DateTime.Now
                                };

                                _db.VideoAnalysisEvents.Add(openEvent);
                                await _db.SaveChangesAsync();

                                needRefreshSnapshot = true;
                            }

                            // 事件写入成功后，使用事件主键作为截图外键，确保“一个事件对应一张图”。
                            int eventIdForSnapshot = openEvent?.Id ?? 0;
                            if (eventIdForSnapshot <= 0)
                            {
                                _logger.LogWarning(
                                    "VideoAnalysisSnapshot skipped because openEvent.Id is invalid. vfId={VideoFileId}, timeSec={TimeSec}, frameIndex={FrameIndex}",
                                    vf.Id, timeSec, snapFrameIndex);
                            }
                            else if (needRefreshSnapshot)
                            {
                                snapshotSeq++;

                                await UpsertEventSnapshotAsync(
                                    vf,
                                    eventIdForSnapshot,
                                    timeSec,
                                    snapFrameIndex,
                                    conf,
                                    peakFrame,
                                    curr,
                                    snapDir,
                                    snapshotSeq);
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

            // ★最终硬裁剪：保证最多5个事件，且每个事件仅保留1张截图
            // 全部分析结束后，按置信度做最终硬裁剪：
            // 只保留前 MaxKeepEvents 个事件，多余事件及其截图一起删除。
            foundEvents = await EnforceTop6EventsHardAsync(vf.Id, MaxKeepEvents);

            int? durationSec = cap.FrameCount > 0 && cap.Fps > 0
                ? (int?)Math.Round(cap.FrameCount / cap.Fps)
                : null;

            sw.Stop();
            return (foundEvents, durationSec, w0, h0, sw.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// 根据视频原始 FPS 和算法配置计算采样间隔。
        /// 例如原视频 25fps、目标采样 5fps，则每 5 帧分析一次；这样能保留短脉冲特征，
        /// 又能显著减少每帧灰度化、差分、轮廓查找带来的 CPU 消耗。
        /// </summary>
                /// <summary>
        /// 根据原始视频 FPS 和算法配置，计算“每隔多少帧采样一次”。
        ///
        /// 例如：
        /// - 原视频 25fps，目标采样率 5fps，则每 5 帧采样 1 次；
        /// - 原视频 30fps，目标采样率 10fps，则每 3 帧采样 1 次。
        ///
        /// 目的：
        /// 在尽量不漏掉短时脉冲的前提下，减少 OpenCV 图像处理成本。
        /// </summary>
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

        /// <summary>
        /// 计算当前采样点的全局亮度特征。
        /// Mean/Std 反映整帧灰度水平，MeanDelta 反映相邻采样帧整体亮度变化，
        /// BrightRatio/BrightDelta 反映高亮像素比例及其变化，用于捕捉火花、闪光这类短时强亮区域。
        /// </summary>
                /// <summary>
        /// 从前一帧和当前帧构建单个采样点的统计特征。
        ///
        /// 这里会计算：
        /// - 当前帧灰度均值 Mean；
        /// - 当前帧灰度标准差 Std；
        /// - 当前帧相对于前一帧的亮度变化 MeanDelta；
        /// - 高亮像素占比 BrightRatio；
        /// - 高亮像素占比变化 BrightDelta。
        ///
        /// 这些统计量是后续候选检测和短脉冲确认的基础输入。
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

            // 高亮阈值随当前帧均值和标准差动态变化，再用配置的上下限夹住，适配不同曝光环境。
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

        /// <summary>
        /// 粗筛候选事件。
        /// 如果全局亮度相对基线或相邻帧差分已经达标，直接把当前点作为候选；
        /// 否则再做局部差分和轮廓提取，尝试定位局部火花区域，并输出 bbox、面积占比和置信度。
        /// </summary>
                /// <summary>
        /// 尝试从当前采样点中检测候选火花/闪光目标。
        ///
        /// 候选不等于最终事件。
        /// 这个方法的职责只是判断“当前帧是否存在明显亮度突增区域”，并尽量给出：
        /// - 候选目标框 BBox；
        /// - 面积占比 AreaRatio；
        /// - 中心点 Center；
        /// - 候选置信度 Confidence。
        ///
        /// 真正是否记为事件，还需要在 ConfirmPulse 中结合时间窗口看它是否满足“短脉冲 + 回落”。
        /// </summary>
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

            // 局部差分：找出前后两帧灰度变化大的区域，再通过形态学闭运算把零散亮点连成轮廓。
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

            // 候选分数综合区域大小、亮点比例变化、整体亮度变化和相对基线抬升。
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
                /// <summary>
        /// 在最近一段时间窗口内确认是否形成了一个有效短脉冲事件。
        ///
        /// 判定思路：
        /// 1. 必须在窗口内看到明显 rise（亮度相对基线抬升）；
        /// 2. 之后还要在合理时间内看到 fall（亮度回落）；
        /// 3. 若持续高亮时间过长，说明更像常亮灯或大面积曝光变化，会 reject；
        /// 4. 若目标在窗口内位移过大，更像移动光源，也会 reject。
        ///
        /// 输出：
        /// - isFlash：更偏“全局闪光”还是“局部火花”；
        /// - confidence：事件置信度；
        /// - bestBox：用于事件记录的最佳目标框；
        /// - peakTimeMs：峰值时刻，用来回取截图。
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
                /// <summary>
        /// 计算中位数。
        ///
        /// 这里单独封装中位数，是因为稳健基线和窗口统计中使用中位数可以减少极端值干扰，
        /// 比简单均值更不容易被瞬时闪光污染。
        /// </summary>
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

                /// <summary>
        /// 在确认窗口中选择“峰值点”索引。
        ///
        /// 优先级：
        /// 1. 优先从存在有效 BBox 的局部候选点中选峰值；
        /// 2. 如果没有可靠局部框，则退化为选择全局 MeanRise 最大点；
        /// 3. 如果 MeanRise 也不明显，再退化为 MeanDelta 最大点。
        ///
        /// 这样做是为了兼顾两类情况：
        /// - 局部火花：更依赖局部候选框；
        /// - 全局闪光：可能根本没有稳定 BBox，需要依赖全局亮度峰值。
        /// </summary>
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

                /// <summary>
        /// 在峰值时间附近选择最合适的目标框。
        ///
        /// 做法是：在窗口内寻找距离 peakTimeMs 最近且 BBox 有效的采样点，
        /// 其目标框作为事件记录的最终 BBox。
        ///
        /// 这样可以避免直接使用非峰值时刻的框，导致事件框偏离真实高亮区域。
        /// </summary>
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

                /// <summary>
        /// 将新的采样点放入时间窗口，并自动裁剪超出窗口上限的旧点。
        ///
        /// 这个窗口用于短脉冲确认，因此只保留最近 windowMaxMs 毫秒的数据即可。
        /// </summary>
        private static void EnqueueWindow(Queue<SamplePoint> q, SamplePoint sp, int windowMaxMs)
        {
            q.Enqueue(sp);
            TrimWindowByMs(q, windowMaxMs);
        }

                /// <summary>
        /// 按时间跨度裁剪窗口，只保留最近 windowMaxMs 毫秒内的采样点。
        /// </summary>
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

                /// <summary>
        /// 将窗口进一步收缩到最近 keepMs 毫秒。
        ///
        /// 常用于 pendingPulse 长时间没有新的候选命中时，
        /// 丢弃过旧统计点，避免上一轮半成品脉冲影响下一轮判定。
        /// </summary>
        private static void TrimWindowToRecent(Queue<SamplePoint> q, int keepMs)
        {
            TrimWindowByMs(q, keepMs);
        }

                /// <summary>
        /// 如果原图宽度超过限制，则按比例缩小；否则直接克隆原图。
        ///
        /// 用途：
        /// 在候选检测阶段降低计算量，尤其是超高分辨率视频，
        /// 可通过缩放减少阈值分割、轮廓提取、差分计算的成本。
        /// </summary>
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

                /// <summary>
        /// 计算灰度图中高于指定阈值的像素占比。
        ///
        /// 该指标用于衡量“整帧或局部区域有多少像素处于高亮状态”，
        /// 是识别全局闪光和局部火花的重要辅助特征。
        /// </summary>
        private static double CalcRatioAboveThreshold(Mat gray, int thr)
        {
            using var mask = new Mat();
            Cv2.Threshold(gray, mask, thr, 255, ThresholdTypes.Binary);
            double count = Cv2.CountNonZero(mask);
            double total = Math.Max(1.0, gray.Width * gray.Height);
            return count / total;
        }

                /// <summary>
        /// 将矩形限制在图像边界内，避免越界。
        ///
        /// 目标框在放大、缩放回原图、加边距等处理中，可能出现坐标越界；
        /// 调用此方法后可确保 ROI 截取或序列化 BBox 时不会抛异常。
        /// </summary>
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
