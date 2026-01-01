using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Preferred.Api.Data;
using Preferred.Api.Models;
using Zwav.Application.Processing;
using Zwav.Infrastructure.Storage;

namespace Preferred.Api.Services
{
    public class ZwavAnalysisAppService : IZwavAnalysisAppService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorage _storage;
        private readonly IAnalysisQueue _queue;

        public ZwavAnalysisAppService(ApplicationDbContext context, IFileStorage storage, IAnalysisQueue queue)
        {
            _context = context;
            _storage = storage;
            _queue = queue;
        }

        public async Task<UploadZwavFileResult> UploadAsync(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length <= 0)
                throw new InvalidOperationException("请选择要上传的文件");

            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file.FileName ?? string.Empty);
            var ext = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(ext) ||
                (!ext.Equals(".zwav", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("只支持 ZWAV(.zwav) 或 ZIP(.zip) 格式的文件");

            var now = DateTime.UtcNow;

            // 用 guid 作为磁盘文件名，避免同名覆盖
            var guid = Guid.NewGuid().ToString("N");

            // 1) 落盘（进度你可以接入你自己的 ProgressWriter，这里先不写库进度）
            var saved = await _storage.SaveAsync(file, guid, progress: null, ct);

            // 2) 写 Tb_ZwavFile（注意：ExtractPath NOT NULL）
            // 策略1：先写 ""，后续 Worker 用 Attach 回写真实 extractDir
            var entity = new ZwavFile
            {
                OriginalName = fileName,
                StoragePath = saved.FullPath,
                ExtractPath = "",              // 关键：NOT NULL 先占位
                FileSize = saved.FileSize,        // 建议存 bytes；你表注释写 Mb，但字段 BIGINT 更适合 bytes
                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavFiles.Add(entity);
            await _context.SaveChangesAsync(ct);

            return new UploadZwavFileResult
            {
                FileId = entity.Id,
                StoragePath = entity.StoragePath,
                OriginalName = entity.OriginalName,
                FileSizeBytes = entity.FileSize,
                CrtTimeUtc = entity.CrtTime
            };
        }
            
        public async Task<(string AnalysisGuid, string Status)> CreateAnalysisByFileIdAsync(
            int fileId,
            bool forceRecreate,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // 1) 读取文件记录
            var zwavFile = await _context.ZwavFiles
                .Where(x => x.Id == fileId)
                .SingleOrDefaultAsync(ct);

            // 2) 扩展名校验（从 OriginalName 判断）
            var ext = Path.GetExtension(zwavFile.OriginalName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(ext) ||
                (!ext.Equals(".zwav", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("只支持 ZWAV(.zwav) 或 ZIP(.zip) 格式的文件");
            }

            // 3) 若不强制重建：复用已有任务（避免重复解析）
            if (!forceRecreate)
            {
                // 你可以按你系统的状态枚举调整这个过滤条件
                var existing = await _context.ZwavAnalyses
                    .Where(a => a.FileId == fileId)
                    .OrderByDescending(a => a.Id)
                    .Select(a => new { a.AnalysisGuid, a.Status })
                    .FirstOrDefaultAsync(ct);

                if (existing != null)
                {
                    // 若已有任务且状态不是 Failed/Canceled，可直接复用
                    // （如你希望 Completed 也强制重建，可在这里排除）
                    if (!string.Equals(existing.Status, "Failed", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(existing.Status, "Canceled", StringComparison.OrdinalIgnoreCase))
                    {
                        return (existing.AnalysisGuid, existing.Status);
                    }
                }
            }

            var guid = Guid.NewGuid().ToString("N");

            var now = DateTime.UtcNow;

            // 4) 先创建 Analysis（Queued/Preparing），保证“进度查询”立刻可用
            var analysis = new ZwavAnalysis
            {
                AnalysisGuid = guid,
                Status = "Queued",       // 这里也可以先用 "Preparing"
                Progress = 0,
                ErrorMessage = null,
                FileId = fileId,
                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavAnalyses.Add(analysis);
            await _context.SaveChangesAsync(ct);

            // 用于限频写库（如果你后面解析阶段还在用它，这里仍可保留）
            var progWriter = new ProgressWriter(_context, analysis.Id, ct);

            try
            {
                // 5) 这里不再做上传落盘，所以可以直接把状态推进到 Queued/Waiting
                await progWriter.UpdateAsync("Queued", 5);

                // 6) 入队：建议入队参数用 analysisGuid 或 analysisId
                await _queue.EnqueueAsync(new ZwavAnalysisQueueItem
                {
                    AnalysisGuid = guid,
                    FileId = fileId,
                    OriginalName = zwavFile.OriginalName,
                    StoragePath = zwavFile.StoragePath,
                    ExtractPath = zwavFile.ExtractPath
                }, ct);
                await progWriter.UpdateAsync("Queued", 10);

                return (guid, "Queued");
            }
            catch (OperationCanceledException)
            {
                await progWriter.UpdateAsync("Canceled", analysis.Progress, "client canceled");
                throw;
            }
            catch (Exception ex)
            {
                await progWriter.UpdateAsync("Failed", analysis.Progress, ex.Message);
                throw;
            }
        }



        /// <summary>
        /// 查询任务状态（直接查 Tb_ZwavAnalysis）
        /// </summary>
        public async Task<AnalysisStatusResponse> GetStatusAsync(string analysisGuid, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);

            if (a == null) return null;

            return new AnalysisStatusResponse
            {
                AnalysisGuid = a.AnalysisGuid,
                Status = a.Status,
                Progress = a.Progress,
                ErrorMessage = a.ErrorMessage,
                TotalRecords = a.TotalRecords,
                RecordSize = a.RecordSize,
                DigitalWords = a.DigitalWords,
                CrtTime = a.CrtTime,
                StartTime = a.StartTime,
                FinishTime = a.FinishTime
            };
        }

        public async Task<PagedResult<AnalysisListItemDto>> QueryAsync(
            string status, string keyword, DateTime? fromUtc, DateTime? toUtc,
            int page, int pageSize, string orderBy, CancellationToken ct)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

            var q = from a in _context.ZwavAnalyses
                    join f in _context.ZwavFiles on a.FileId equals f.Id
                    select new { a, f };

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(x => x.a.Status == status);

            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(x => x.f.OriginalName.Contains(keyword) || x.a.AnalysisGuid.Contains(keyword));

            if (fromUtc.HasValue)
                q = q.Where(x => x.a.CrtTime >= fromUtc.Value);

            if (toUtc.HasValue)
                q = q.Where(x => x.a.CrtTime <= toUtc.Value);

            q = orderBy switch
            {
                "UpdTimeDesc" => q.OrderByDescending(x => x.a.UpdTime),
                _ => q.OrderByDescending(x => x.a.CrtTime)
            };

            var totalLong = await q.LongCountAsync(ct);
            var total = totalLong > int.MaxValue ? int.MaxValue : (int)totalLong;

            var items = await q.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AnalysisListItemDto
                {
                    AnalysisGuid = x.a.AnalysisGuid,
                    Status = x.a.Status,
                    Progress = x.a.Progress,
                    ErrorMessage = x.a.ErrorMessage,
                    OriginalName = x.f.OriginalName,
                    FileSize = x.f.FileSize,
                    CrtTime = x.a.CrtTime,
                    UpdTime = x.a.UpdTime,
                    StartTime = x.a.StartTime,
                    FinishTime = x.a.FinishTime
                })
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            return new PagedResult<AnalysisListItemDto>
            {
                Data = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<AnalysisDetailDto> GetDetailAsync(string analysisGuid, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return null;

            var f = await _context.ZwavFiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == a.FileId, ct);

            var c = await _context.ZwavCfgs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == a.Id, ct);

            return new AnalysisDetailDto
            {
                AnalysisGuid = a.AnalysisGuid,
                Status = a.Status,
                Progress = a.Progress,
                ErrorMessage = a.ErrorMessage,
                TotalRecords = a.TotalRecords,
                RecordSize = a.RecordSize,
                DigitalWords = a.DigitalWords,
                CrtTime = a.CrtTime,
                UpdTime = a.UpdTime,
                StartTime = a.StartTime,
                FinishTime = a.FinishTime,
                File = f == null ? null : new FileDto
                {
                    Id = f.Id,
                    OriginalName = f.OriginalName,
                    FileSize = f.FileSize,
                    StoragePath = f.StoragePath,
                    ExtractPath = f.ExtractPath
                },
                CfgSummary = c == null ? null : new CfgSummaryDto
                {
                    StationName = c.StationName,
                    DeviceId = c.DeviceId,
                    AnalogCount = c.AnalogCount,
                    DigitalCount = c.DigitalCount,
                    FrequencyHz = c.FrequencyHz,
                    TimeMul = c.TimeMul,
                    StartTimeRaw = c.StartTimeRaw,
                    TriggerTimeRaw = c.TriggerTimeRaw
                }
            };
        }

        public async Task<CfgDto> GetCfgAsync(string analysisGuid, bool includeText, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return null;

            var c = await _context.ZwavCfgs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == a.Id, ct);
            if (c == null) return null;

            return new CfgDto
            {
                StationName = c.StationName,
                DeviceId = c.DeviceId,
                Revision = c.Revision,
                AnalogCount = c.AnalogCount,
                DigitalCount = c.DigitalCount,
                FrequencyHz = c.FrequencyHz,
                TimeMul = c.TimeMul,
                StartTimeRaw = c.StartTimeRaw,
                TriggerTimeRaw = c.TriggerTimeRaw,
                FormatType = c.FormatType,
                DataType = c.DataType,
                SampleRateJson = c.SampleRateJson,
                FullCfgText = includeText ? c.FullCfgText : null
            };
        }

        public async Task<ChannelDto[]> GetChannelsAsync(string analysisGuid, string type, bool enabledOnly, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return null;

            var q = _context.ZwavChannels.AsNoTracking().Where(x => x.AnalysisId == a.Id);

            if (enabledOnly)
                q = q.Where(x => x.IsEnable == 1);

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("All", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.ChannelType == type);

            return await q.OrderBy(x => x.ChannelIndex)
                .Select(x => new ChannelDto
                {
                    ChannelIndex = x.ChannelIndex,
                    ChannelType = x.ChannelType,
                    ChannelCode = x.ChannelCode,
                    ChannelName = x.ChannelName,
                    Phase = x.Phase,
                    Unit = x.Unit,
                    RatioA = x.RatioA,
                    OffsetB = x.OffsetB,
                    Skew = x.Skew,
                    IsEnable = x.IsEnable
                })
                .ToArrayAsync(ct);
        }

        public async Task<HdrDto> GetHdrAsync(string analysisGuid, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return null;

            var h = await _context.ZwavHdrs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisId == a.Id, ct);
            if (h == null) return null;

            return new HdrDto
            {
                FaultStartTime = h.FaultStartTime,
                FaultKeepingTime = h.FaultKeepingTime,
                DeviceInfoJson = h.DeviceInfoJson,
                TripInfoJSON = h.TripInfoJSON,
                FaultInfoJson = h.FaultInfoJson,
                DigitalStatusJson = h.DigitalStatusJson,
                DigitalEventJson = h.DigitalEventJson,
                SettingValueJson = h.SettingValueJson,
                RelayEnaValueJSON = h.RelayEnaValueJSON
            };
        }

        // 纯 LINQ 版（不写 SQL）
        private static int[] ParseIndices(string csv, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<int>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var v) ? v : -1)
                .Where(v => v >= min && v <= max)
                .Distinct()
                .ToArray();
        }

        private static readonly System.Reflection.PropertyInfo[] ZwavDataChannelProps =
            Enumerable.Range(1, 70).Select(i => typeof(ZwavData).GetProperty($"Channel{i}")).ToArray();

        private static readonly System.Reflection.PropertyInfo[] ZwavDataDigitalProps =
            Enumerable.Range(1, 50).Select(i => typeof(ZwavData).GetProperty($"Digital{i}")).ToArray();

        private static double ReadAnalog(ZwavData row, int idx)
        {
            var p = ZwavDataChannelProps[idx - 1];
            if (p == null) return 0;
            var v = p.GetValue(row);
            return v == null ? 0 : Convert.ToDouble(v);
        }

        private static short ReadDigital(ZwavData row, int idx)
        {
            var p = ZwavDataDigitalProps[idx - 1];
            if (p == null) return 0;
            var v = p.GetValue(row);
            return v == null ? (short)0 : Convert.ToInt16(v);
        }

        public async Task<WaveDataPageDto> GetWaveDataAsync(
            string analysisGuid,
            int? fromSample, int? toSample,
            int? offset, int? limit,
            string channels, string digitals,
            int downSample,
            CancellationToken ct)
        {
            downSample = downSample <= 0 ? 1 : downSample;

            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return null;

            var ch = ParseIndices(channels, 1, 70);
            var dg = ParseIndices(digitals, 1, 50);

            if (ch.Length == 0 && dg.Length == 0)
                ch = new[] { 1 };

            int take = limit.GetValueOrDefault(2000);
            take = Math.Clamp(take, 1, 20000);

            int from, to;
            if (fromSample.HasValue && toSample.HasValue)
            {
                from = Math.Min(fromSample.Value, toSample.Value);
                to = Math.Max(fromSample.Value, toSample.Value);
            }
            else
            {
                from = offset.GetValueOrDefault(0);
                to = int.MaxValue;
            }

            var q = _context.ZwavDatas.AsNoTracking()
                .Where(x => x.AnalysisId == a.Id && x.SampleNo >= from);

            if (to != int.MaxValue)
                q = q.Where(x => x.SampleNo <= to);

            if (downSample > 1)
                q = q.Where(x => (x.SampleNo % downSample) == 0);

            var rows = await q.OrderBy(x => x.SampleNo)
                .Take(take)
                .ToListAsync(ct);

            var dtoRows = rows.Select(r => new WaveDataRowDto
            {
                SampleNo = r.SampleNo,
                TimeRaw = r.TimeRaw,
                Analog = ch.Select(idx => ReadAnalog(r, idx)).ToArray(),
                Digital = dg.Select(idx => ReadDigital(r, idx)).ToArray()
            }).ToArray();

            return new WaveDataPageDto
            {
                FromSample = from,
                ToSample = (to == int.MaxValue) ? (dtoRows.LastOrDefault()?.SampleNo ?? (from + take)) : to,
                DownSample = downSample,
                Channels = ch,
                Digitals = dg,
                Rows = dtoRows
            };
        }

        public async Task<bool> CancelAsync(string analysisGuid, CancellationToken ct)
        {
            // 最小可用实现：把状态标记为 Canceled（不改 Worker 也能表现为取消）
            var a = await _context.ZwavAnalyses.FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return false;

            // 如果已经完成/失败就不取消
            if (a.Status == "Ready" || a.Status == "Failed") return false;

            a.Status = "Canceled";
            a.ErrorMessage = "client canceled";
            a.UpdTime = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteAsync(string analysisGuid, bool deleteFile, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses.FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return false;

            // 取 file（可选删除物理文件）
            var f = await _context.ZwavFiles.FirstOrDefaultAsync(x => x.Id == a.FileId, ct);

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            // 先删子表
            var datas = _context.ZwavDatas.Where(x => x.AnalysisId == a.Id);
            _context.ZwavDatas.RemoveRange(datas);

            var chs = _context.ZwavChannels.Where(x => x.AnalysisId == a.Id);
            _context.ZwavChannels.RemoveRange(chs);

            var cfg = await _context.ZwavCfgs.FirstOrDefaultAsync(x => x.AnalysisId == a.Id, ct);
            if (cfg != null) _context.ZwavCfgs.Remove(cfg);

            var hdr = await _context.ZwavHdrs.FirstOrDefaultAsync(x => x.AnalysisId == a.Id, ct);
            if (hdr != null) _context.ZwavHdrs.Remove(hdr);

            // 再删主表
            _context.ZwavAnalyses.Remove(a);

            // 是否删文件记录
            if (deleteFile && f != null)
                _context.ZwavFiles.Remove(f);

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // 物理文件删除（不建议在事务内做）
            if (deleteFile && f != null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(f.StoragePath) && File.Exists(f.StoragePath))
                        File.Delete(f.StoragePath);
                }
                catch
                {
                    // 物理文件删除失败不影响数据库删除结果（按需你也可记录日志）
                }
            }

            return true;
        }

    }
}