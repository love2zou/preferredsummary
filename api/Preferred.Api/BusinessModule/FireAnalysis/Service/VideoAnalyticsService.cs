using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Video.Application.Dto;
using Video.Application.Processing;

namespace Preferred.Api.Services
{
    public sealed class VideoAnalyticsService : IVideoAnalyticsService
    {
        private readonly ApplicationDbContext _db;
        private readonly IVideoAnalysisQueue _queue;
        private readonly FileStorageConfig _fs;
        private readonly ILogger<VideoAnalyticsService> _logger;

        public VideoAnalyticsService(
            ApplicationDbContext db,
            IVideoAnalysisQueue queue,
            IOptions<FileStorageConfig> fsOptions,
            ILogger<VideoAnalyticsService> logger)
        {
            _db = db;
            _queue = queue;
            _fs = fsOptions.Value;
            _logger = logger;
        }

        public async Task<CreateJobResultDto> CreateJobAsync(string algoParamsJson, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(algoParamsJson))
                throw new ArgumentException("algoParamsJson 不能为空");

            var now = DateTime.Now;

            // JobNo：尽量确保唯一
            var jobNo = "VJOB" + now.ToString("yyyyMMddHHmmssfff");

            var job = new VideoAnalysisJob
            {
                JobNo = jobNo,
                AlgoCode = "spark_v1", // 默认算法
                AlgoParamsJson = algoParamsJson,

                Status = 0,             // pending
                Progress = 0,
                TotalVideoCount = 0,    // 0 表示未 Close（持续上传模式）
                FinishedVideoCount = 0,
                TotalEventCount = 0,

                ErrorMessage = null,
                StartTime = null,
                FinishTime = null,

                CrtTime = now,
                UpdTime = now
            };

            _db.VideoAnalysisJobs.Add(job);
            await _db.SaveChangesAsync(ct);

            return new CreateJobResultDto
            {
                JobId = job.Id,
                JobNo = job.JobNo
            };
        }

        public async Task<UploadVideoResultDto> UploadAndEnqueueAsync(string jobNo, IFormFile file, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                throw new ArgumentException("jobNo 不能为空");
            if (file == null || file.Length <= 0)
                throw new ArgumentException("file 不能为空");

            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null)
                throw new ArgumentException("任务不存在: " + jobNo);

            // 取消/完成/失败：不可上传
            if (job.Status == 2 || job.Status == 3 || job.Status == 4)
                throw new InvalidOperationException("任务当前状态不可上传");

            // Close 后禁止上传：用 TotalVideoCount>0 作为“已关闭上传”的标记
            if (job.TotalVideoCount > 0)
                throw new InvalidOperationException("任务已关闭上传，不再接收新视频");

            var now = DateTime.Now;

            // 任务目录：{root}/{jobNo}/videos
            string jobDir = Path.Combine(_fs.VideoRootPath, jobNo);
            string videoDir = Path.Combine(jobDir, "videos");
            Directory.CreateDirectory(videoDir);

            string ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".mp4";

            string saveName = $"{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(videoDir, saveName);

            // 保存文件（失败要清理半成品）
            try
            {
                await using var fs = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    useAsync: true);

                await file.CopyToAsync(fs, ct);
            }
            catch
            {
                TryDeleteFileQuiet(filePath);
                throw;
            }

            // SeqNo 自增
            int nextSeq = 1;
            var maxSeq = await _db.VideoAnalysisFiles
                .Where(x => x.JobId == job.Id)
                .Select(x => (int?)x.SeqNo)
                .MaxAsync(ct);
            if (maxSeq.HasValue) nextSeq = maxSeq.Value + 1;

            var vf = new VideoAnalysisFile
            {
                JobId = job.Id,
                FileName = file.FileName,
                FilePath = filePath,

                DurationSec = null,
                Width = null,
                Height = null,

                Status = 0, // pending
                ErrorMessage = null,

                SeqNo = nextSeq,
                CrtTime = now,
                UpdTime = now
            };

            _db.VideoAnalysisFiles.Add(vf);

            // job 进入处理中
            if (job.Status == 0)
            {
                job.Status = 1;
                job.StartTime ??= now;
            }

            job.UpdTime = now;

            await _db.SaveChangesAsync(ct);

            await _queue.EnqueueAsync(vf.Id, ct);

            return new UploadVideoResultDto
            {
                JobNo = job.JobNo,
                JobId = job.Id,
                FileId = vf.Id,
                SeqNo = vf.SeqNo,
                FileName = vf.FileName,
                FilePath = vf.FilePath,
                EnqueueMessage = "queued"
            };
        }

        public async Task<CreateJobResultDto> CreateAndEnqueueAsync(IFormFile[] files, string algoParamsJson, CancellationToken ct)
        {
            if (files == null || files.Length == 0)
                throw new ArgumentException("files 不能为空");
            if (string.IsNullOrWhiteSpace(algoParamsJson))
                throw new ArgumentException("algoParamsJson 不能为空");

            var job = await CreateJobAsync(algoParamsJson, ct);

            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                if (f == null || f.Length == 0) continue;

                await UploadAndEnqueueAsync(job.JobNo, f, ct);
            }

            // 旧接口是否自动 close：按你需求决定
            // await CloseJobAsync(job.JobNo, ct);

            return job;
        }

        public async Task<ReanalyzeResultDto> ReanalyzeFilesAsync(string jobNo, int[] fileIds, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                throw new ArgumentException("jobNo 不能为空");
            if (fileIds == null || fileIds.Length == 0)
                throw new ArgumentException("fileIds 不能为空");

            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null)
                throw new ArgumentException("任务不存在: " + jobNo);

            if (job.Status == 4)
                throw new InvalidOperationException("任务已取消，不能重新分析");

            var ids = fileIds.Where(x => x > 0).Distinct().ToArray();
            if (ids.Length == 0)
                throw new ArgumentException("fileIds 不合法");

            var files = await _db.VideoAnalysisFiles
                .Where(x => x.JobId == job.Id && ids.Contains(x.Id))
                .ToListAsync(ct);

            if (files.Count == 0)
                return new ReanalyzeResultDto { RequeuedCount = 0, ClearedEventCount = 0, ClearedSnapshotCount = 0 };

            var eventIds = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == job.Id && ids.Contains(e.VideoFileId))
                .Select(e => e.Id)
                .ToListAsync(ct);

            int clearedSnap = 0;
            if (eventIds.Count > 0)
            {
                clearedSnap = await _db.VideoAnalysisSnapshots
                    .Where(s => eventIds.Contains(s.EventId))
                    .CountAsync(ct);
            }

            int clearedEvt = eventIds.Count;

            var now = DateTime.Now;

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                if (eventIds.Count > 0)
                {
                    var snaps = _db.VideoAnalysisSnapshots.Where(s => eventIds.Contains(s.EventId));
                    _db.VideoAnalysisSnapshots.RemoveRange(snaps);

                    var evts = _db.VideoAnalysisEvents.Where(e => eventIds.Contains(e.Id));
                    _db.VideoAnalysisEvents.RemoveRange(evts);
                }

                foreach (var vf in files)
                {
                    vf.Status = 0;
                    vf.ErrorMessage = null;
                    vf.EventCount = null;
                    vf.AnalyzeMs = null;
                    vf.DurationSec = null;
                    vf.Width = null;
                    vf.Height = null;
                    vf.UpdTime = now;
                }

                job.Status = 1;
                job.ErrorMessage = null;
                job.FinishTime = null;
                job.StartTime ??= now;
                job.UpdTime = now;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            foreach (var vf in files)
            {
                await _queue.EnqueueAsync(vf.Id, ct);
            }

            await RecomputeJobAggregateAsync(job.Id, ct);

            return new ReanalyzeResultDto
            {
                RequeuedCount = files.Count,
                ClearedEventCount = clearedEvt,
                ClearedSnapshotCount = clearedSnap
            };
        }

        private async Task RecomputeJobAggregateAsync(int jobId, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId, ct);
            if (job == null) return;
            if (job.Status == 4) return;

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
                    job.Status = 2;
                    job.Progress = 100;
                    job.FinishTime = DateTime.Now;
                }
                else
                {
                    job.Status = 1;
                    job.FinishTime = null;
                }
            }
            else
            {
                job.Progress = totalUploaded <= 0 ? 0 : (int)Math.Round(finished * 100.0 / totalUploaded);
                job.Status = (byte)(totalUploaded > 0 ? 1 : 0);
                job.FinishTime = null;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<bool> CloseJobAsync(string jobNo, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null) return false;

            // done/failed/cancelled 不需要 close
            if (job.Status == 2 || job.Status == 3 || job.Status == 4) return false;

            // 已 close
            if (job.TotalVideoCount > 0) return true;

            var total = await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.JobId == job.Id)
                .CountAsync(ct);

            job.TotalVideoCount = total; // 作为 Close 标记（固定总数）
            job.UpdTime = DateTime.Now;

            if (job.Status == 0)
            {
                job.Status = 1;
                job.StartTime ??= DateTime.Now;
            }

            await _db.SaveChangesAsync(ct);

            await TryFinalizeJobIfClosedAsync(job.Id, ct);
            return true;
        }

        private async Task TryFinalizeJobIfClosedAsync(int jobId, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId, ct);
            if (job == null) return;

            if (job.Status == 4) return; // cancelled
            if (job.TotalVideoCount <= 0) return; // not closed
            if (job.Status == 2 || job.Status == 3) return;

            var files = await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.JobId == jobId)
                .Select(x => new { x.Status })
                .ToListAsync(ct);

            int finished = files.Count(x => x.Status == 2);
            int failed = files.Count(x => x.Status == 3);

            int totalEvents = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == jobId)
                .CountAsync(ct);

            job.FinishedVideoCount = finished;
            job.TotalEventCount = totalEvents;

            job.Progress = job.TotalVideoCount <= 0
                ? 0
                : (int)Math.Round(finished * 100.0 / job.TotalVideoCount);

            job.UpdTime = DateTime.Now;

            if (finished + failed >= job.TotalVideoCount)
            {
                job.Status = 2; // done
                job.Progress = 100;
                job.FinishTime = DateTime.Now;
                job.UpdTime = DateTime.Now;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<bool> CancelJobAsync(string jobNo, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null) return false;

            if (job.Status == 2 || job.Status == 3) return false;

            job.Status = 4;
            job.UpdTime = DateTime.Now;
            job.FinishTime ??= DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<DeleteJobResultDto> DeleteJobAsync(string jobNo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return new DeleteJobResultDto { Success = false, Message = "任务编号不能为空", FailedFiles = Array.Empty<string>() };

            var job = await _db.VideoAnalysisJobs.FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null)
                return new DeleteJobResultDto { Success = false, Message = "任务不存在", FailedFiles = Array.Empty<string>() };

            // 标取消，减少并发写入
            if (job.Status == 1)
            {
                job.Status = 4;
                job.UpdTime = DateTime.Now;
                await _db.SaveChangesAsync(ct);
            }

            var failed = new List<string>();

            var videoPaths = await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.JobId == job.Id)
                .Select(x => x.FilePath)
                .ToListAsync(ct);

            var eventIds = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == job.Id)
                .Select(e => e.Id)
                .ToListAsync(ct);

            List<string> snapPaths = new List<string>();
            if (eventIds.Count > 0)
            {
                snapPaths = await _db.VideoAnalysisSnapshots.AsNoTracking()
                    .Where(s => eventIds.Contains(s.EventId))
                    .Select(s => s.ImagePath)
                    .ToListAsync(ct);
            }

            string jobDir = Path.Combine(_fs.VideoRootPath, jobNo);

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                if (eventIds.Count > 0)
                {
                    var snaps = _db.VideoAnalysisSnapshots.Where(s => eventIds.Contains(s.EventId));
                    _db.VideoAnalysisSnapshots.RemoveRange(snaps);

                    var events = _db.VideoAnalysisEvents.Where(e => e.JobId == job.Id);
                    _db.VideoAnalysisEvents.RemoveRange(events);
                }

                var files = _db.VideoAnalysisFiles.Where(f => f.JobId == job.Id);
                _db.VideoAnalysisFiles.RemoveRange(files);

                _db.VideoAnalysisJobs.Remove(job);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            foreach (var p in videoPaths.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                TryDeleteFile(p, failed);

            foreach (var p in snapPaths.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                TryDeleteFile(p, failed);

            TryDeleteDirectory(jobDir, failed);

            return new DeleteJobResultDto
            {
                Success = true,
                Message = failed.Count == 0 ? "删除成功" : $"删除成功（部分文件/目录删除失败：{failed.Count}）",
                FailedFiles = failed.ToArray()
            };
        }

        public async Task<JobDetailDto> GetJobAsync(string jobNo, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null) return null;

            var files = await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.JobId == job.Id)
                .OrderBy(x => x.SeqNo)
                .ToListAsync(ct);

            return JobDetailDto.From(job, files);
        }

        public async Task<List<EventDto>> GetJobEventsAsync(string jobNo, CancellationToken ct)
        {
            var job = await _db.VideoAnalysisJobs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.JobNo == jobNo, ct);
            if (job == null) return new List<EventDto>();

            var events = await _db.VideoAnalysisEvents.AsNoTracking()
                .Where(e => e.JobId == job.Id)
                .OrderBy(e => e.VideoFileId)
                .ThenBy(e => e.SeqNo)
                .ToListAsync(ct);

            return events.Select(EventDto.From).ToList();
        }

        public async Task<List<SnapshotDto>> GetEventSnapshotsAsync(int eventId, CancellationToken ct)
        {
            var snaps = await _db.VideoAnalysisSnapshots.AsNoTracking()
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.SeqNo)
                .ToListAsync(ct);

            return snaps.Select(SnapshotDto.From).ToList();
        }

        public async Task<string> GetSnapshotPathAsync(int snapshotId, CancellationToken ct)
        {
            return await _db.VideoAnalysisSnapshots.AsNoTracking()
                .Where(x => x.Id == snapshotId)
                .Select(x => x.ImagePath)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<string> GetVideoPathAsync(int fileId, CancellationToken ct)
        {
            return await _db.VideoAnalysisFiles.AsNoTracking()
                .Where(x => x.Id == fileId)
                .Select(x => x.FilePath)
                .FirstOrDefaultAsync(ct);
        }

        private static void TryDeleteFile(string path, List<string> failed)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                failed.Add(path);
            }
        }

        private static void TryDeleteDirectory(string dir, List<string> failed)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                failed.Add(dir);
            }
        }

        private static void TryDeleteFileQuiet(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }
}
