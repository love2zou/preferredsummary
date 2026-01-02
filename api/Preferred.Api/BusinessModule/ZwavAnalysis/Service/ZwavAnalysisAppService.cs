using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Preferred.Api.Data;
using Zwav.Application.Parsing;
using Preferred.Api.Models;
using Zwav.Application.Processing;
using Zwav.Infrastructure.Storage;
using System.Linq.Expressions;

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
            int page, int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

            var q = from a in _context.ZwavAnalyses
                    join f in _context.ZwavFiles on a.FileId equals f.Id
                    select new { a, f };

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(x => x.a.Status == status);

            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(x => x.f.OriginalName.Contains(keyword));

            if (fromUtc.HasValue)
                q = q.Where(x => x.a.CrtTime >= fromUtc.Value);

            if (toUtc.HasValue)
                q = q.Where(x => x.a.CrtTime <= toUtc.Value);

            var totalLong = await q.LongCountAsync();
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
                }).OrderByDescending(q => q.UpdTime)
                .ToListAsync();

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
                DeviceInfoJson = JsonSerializer.Deserialize<List<NameValue>>(h.DeviceInfoJson),
                TripInfoJSON = JsonSerializer.Deserialize<List<TripInfo>>(h.TripInfoJSON),
                FaultInfoJson = JsonSerializer.Deserialize<List<FaultInfo>>(h.FaultInfoJson),
                DigitalStatusJson = JsonSerializer.Deserialize<List<NameValue>>(h.DigitalStatusJson),
                DigitalEventJson = JsonSerializer.Deserialize<List<DigitalEvent>>(h.DigitalEventJson),
                SettingValueJson = JsonSerializer.Deserialize<List<SettingValue>>(h.SettingValueJson),
                RelayEnaValueJSON = JsonSerializer.Deserialize<List<RelayEnaValue>>(h.RelayEnaValueJSON)
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

        


        private static readonly Func<WaveRowRaw, double>[] ZwavAnalogGetters =
            Enumerable.Range(1, 70).Select(BuildAnalogGetter).ToArray();

        private static readonly Func<WaveRowRaw, short>[] ZwavDigitalGetters =
            Enumerable.Range(1, 50).Select(BuildDigitalGetter).ToArray();

        private static Func<WaveRowRaw, double> BuildAnalogGetter(int i)
        {
            var prop = typeof(WaveRowRaw).GetProperty($"Channel{i}", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return _ => 0d;

            var row = Expression.Parameter(typeof(WaveRowRaw), "row");
            var access = Expression.Property(row, prop);                       // row.Channel{i}
            var toObj = Expression.Convert(access, typeof(object));            // box
            var call = Expression.Condition(
                Expression.Equal(toObj, Expression.Constant(null)),
                Expression.Constant(0d),
                Expression.Convert(Expression.Call(typeof(Convert), nameof(Convert.ToDouble), null, toObj), typeof(double))
            );

            return Expression.Lambda<Func<WaveRowRaw, double>>(call, row).Compile();
        }

        private static Func<WaveRowRaw, short> BuildDigitalGetter(int i)
        {
            var prop = typeof(WaveRowRaw).GetProperty($"Digital{i}", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return _ => (short)0;

            var row = Expression.Parameter(typeof(WaveRowRaw), "row");
            var access = Expression.Property(row, prop);
            var toObj = Expression.Convert(access, typeof(object));
            var call = Expression.Condition(
                Expression.Equal(toObj, Expression.Constant(null)),
                Expression.Constant((short)0),
                Expression.Convert(Expression.Call(typeof(Convert), nameof(Convert.ToInt16), null, toObj), typeof(short))
            );

            return Expression.Lambda<Func<WaveRowRaw, short>>(call, row).Compile();
        }

        private static double ReadAnalog(WaveRowRaw row, int idx)
        {
            if (idx < 1 || idx > ZwavAnalogGetters.Length) return 0d;
            return ZwavAnalogGetters[idx - 1](row);
        }

        private static short ReadDigital(WaveRowRaw row, int idx)
        {
            if (idx < 1 || idx > ZwavDigitalGetters.Length) return (short)0;
            return ZwavDigitalGetters[idx - 1](row);
        }

        private static double[] ReadAnalogs(WaveRowRaw row, int[] indices)
        {
            if (indices == null || indices.Length == 0) return Array.Empty<double>();

            var arr = new double[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                var idx = indices[i];
                // 防御：避免 idx 越界导致 switch default 以外的异常（你 switch 已 default=0，其实这里可省）
                if (idx < 1 || idx > 70) { arr[i] = 0; continue; }
                arr[i] = row.GetAnalog(idx);
            }
            return arr;
        }

        private static short[] ReadDigitals(WaveRowRaw row, int[] indices)
        {
            if (indices == null || indices.Length == 0) return Array.Empty<short>();

            var arr = new short[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                var idx = indices[i];
                if (idx < 1 || idx > 50) { arr[i] = 0; continue; }
                arr[i] = row.GetDigital(idx);
            }
            return arr;
        }

        public async Task<WaveDataPageDto> GetWaveDataAsync(
            string analysisGuid,
            int? fromSample, int? toSample,
            int? offset, int? limit,
            string channels, string digitals,
            int downSample)
        {
            downSample = downSample <= 0 ? 1 : downSample;

            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid);
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
                .Select(x => new WaveRowRaw
                {
                    SampleNo = x.SampleNo,
                    TimeRaw = x.TimeRaw,
                    Channel1 = x.Channel1 != null ? (double?)Math.Round(x.Channel1.Value, 3) : null,
                    Channel2 = x.Channel2 != null ? (double?)Math.Round(x.Channel2.Value, 3) : null,
                    Channel3 = x.Channel3 != null ? (double?)Math.Round(x.Channel3.Value, 3) : null,
                    Channel4 = x.Channel4 != null ? (double?)Math.Round(x.Channel4.Value, 3) : null,
                    Channel5 = x.Channel5 != null ? (double?)Math.Round(x.Channel5.Value, 3) : null,
                    Channel6 = x.Channel6 != null ? (double?)Math.Round(x.Channel6.Value, 3) : null,
                    Channel7 = x.Channel7 != null ? (double?)Math.Round(x.Channel7.Value, 3) : null,
                    Channel8 = x.Channel8 != null ? (double?)Math.Round(x.Channel8.Value, 3) : null,
                    Channel9 = x.Channel9 != null ? (double?)Math.Round(x.Channel9.Value, 3) : null,
                    Channel10 = x.Channel10 != null ? (double?)Math.Round(x.Channel10.Value, 3) : null,
                    Channel11 = x.Channel11 != null ? (double?)Math.Round(x.Channel11.Value, 3) : null,
                    Channel12 = x.Channel12 != null ? (double?)Math.Round(x.Channel12.Value, 3) : null,
                    Channel13 = x.Channel13 != null ? (double?)Math.Round(x.Channel13.Value, 3) : null,
                    Channel14 = x.Channel14 != null ? (double?)Math.Round(x.Channel14.Value, 3) : null,
                    Channel15 = x.Channel15 != null ? (double?)Math.Round(x.Channel15.Value, 3) : null,
                    Channel16 = x.Channel16 != null ? (double?)Math.Round(x.Channel16.Value, 3) : null,
                    Channel17 = x.Channel17 != null ? (double?)Math.Round(x.Channel17.Value, 3) : null,
                    Channel18 = x.Channel18 != null ? (double?)Math.Round(x.Channel18.Value, 3) : null,
                    Channel19 = x.Channel19 != null ? (double?)Math.Round(x.Channel19.Value, 3) : null,
                    Channel20 = x.Channel20 != null ? (double?)Math.Round(x.Channel20.Value, 3) : null,
                    Channel21 = x.Channel21 != null ? (double?)Math.Round(x.Channel21.Value, 3) : null,
                    Channel22 = x.Channel22 != null ? (double?)Math.Round(x.Channel22.Value, 3) : null,
                    Channel23 = x.Channel23 != null ? (double?)Math.Round(x.Channel23.Value, 3) : null,
                    Channel24 = x.Channel24 != null ? (double?)Math.Round(x.Channel24.Value, 3) : null,
                    Channel25 = x.Channel25 != null ? (double?)Math.Round(x.Channel25.Value, 3) : null,
                    Channel26 = x.Channel26 != null ? (double?)Math.Round(x.Channel26.Value, 3) : null,
                    Channel27 = x.Channel27 != null ? (double?)Math.Round(x.Channel27.Value, 3) : null,
                    Channel28 = x.Channel28 != null ? (double?)Math.Round(x.Channel28.Value, 3) : null,
                    Channel29 = x.Channel29 != null ? (double?)Math.Round(x.Channel29.Value, 3) : null,
                    Channel30 = x.Channel30 != null ? (double?)Math.Round(x.Channel30.Value, 3) : null,
                    Channel31 = x.Channel31 != null ? (double?)Math.Round(x.Channel31.Value, 3) : null,
                    Channel32 = x.Channel32 != null ? (double?)Math.Round(x.Channel32.Value, 3) : null,
                    Channel33 = x.Channel33 != null ? (double?)Math.Round(x.Channel33.Value, 3) : null,
                    Channel34 = x.Channel34 != null ? (double?)Math.Round(x.Channel34.Value, 3) : null,
                    Channel35 = x.Channel35 != null ? (double?)Math.Round(x.Channel35.Value, 3) : null,
                    Channel36 = x.Channel36 != null ? (double?)Math.Round(x.Channel36.Value, 3) : null,
                    Channel37 = x.Channel37 != null ? (double?)Math.Round(x.Channel37.Value, 3) : null,
                    Channel38 = x.Channel38 != null ? (double?)Math.Round(x.Channel38.Value, 3) : null,
                    Channel39 = x.Channel39 != null ? (double?)Math.Round(x.Channel39.Value, 3) : null,
                    Channel40 = x.Channel40 != null ? (double?)Math.Round(x.Channel40.Value, 3) : null,
                    Channel41 = x.Channel41 != null ? (double?)Math.Round(x.Channel41.Value, 3) : null,
                    Channel42 = x.Channel42 != null ? (double?)Math.Round(x.Channel42.Value, 3) : null,
                    Channel43 = x.Channel43 != null ? (double?)Math.Round(x.Channel43.Value, 3) : null,
                    Channel44 = x.Channel44 != null ? (double?)Math.Round(x.Channel44.Value, 3) : null,
                    Channel45 = x.Channel45 != null ? (double?)Math.Round(x.Channel45.Value, 3) : null,
                    Channel46 = x.Channel46 != null ? (double?)Math.Round(x.Channel46.Value, 3) : null,
                    Channel47 = x.Channel47 != null ? (double?)Math.Round(x.Channel47.Value, 3) : null,
                    Channel48 = x.Channel48 != null ? (double?)Math.Round(x.Channel48.Value, 3) : null,
                    Channel49 = x.Channel49 != null ? (double?)Math.Round(x.Channel49.Value, 3) : null,
                    Channel50 = x.Channel50 != null ? (double?)Math.Round(x.Channel50.Value, 3) : null,
                    Channel51 = x.Channel51 != null ? (double?)Math.Round(x.Channel51.Value, 3) : null,
                    Channel52 = x.Channel52 != null ? (double?)Math.Round(x.Channel52.Value, 3) : null,
                    Channel53 = x.Channel53 != null ? (double?)Math.Round(x.Channel53.Value, 3) : null,
                    Channel54 = x.Channel54 != null ? (double?)Math.Round(x.Channel54.Value, 3) : null,
                    Channel55 = x.Channel55 != null ? (double?)Math.Round(x.Channel55.Value, 3) : null,
                    Channel56 = x.Channel56 != null ? (double?)Math.Round(x.Channel56.Value, 3) : null,
                    Channel57 = x.Channel57 != null ? (double?)Math.Round(x.Channel57.Value, 3) : null,
                    Channel58 = x.Channel58 != null ? (double?)Math.Round(x.Channel58.Value, 3) : null,
                    Channel59 = x.Channel59 != null ? (double?)Math.Round(x.Channel59.Value, 3) : null,
                    Channel60 = x.Channel60 != null ? (double?)Math.Round(x.Channel60.Value, 3) : null,
                    Channel61 = x.Channel61 != null ? (double?)Math.Round(x.Channel61.Value, 3) : null,
                    Channel62 = x.Channel62 != null ? (double?)Math.Round(x.Channel62.Value, 3) : null,
                    Channel63 = x.Channel63 != null ? (double?)Math.Round(x.Channel63.Value, 3) : null,
                    Channel64 = x.Channel64 != null ? (double?)Math.Round(x.Channel64.Value, 3) : null,
                    Channel65 = x.Channel65 != null ? (double?)Math.Round(x.Channel65.Value, 3) : null,
                    Channel66 = x.Channel66 != null ? (double?)Math.Round(x.Channel66.Value, 3) : null,
                    Channel67 = x.Channel67 != null ? (double?)Math.Round(x.Channel67.Value, 3) : null,
                    Channel68 = x.Channel68 != null ? (double?)Math.Round(x.Channel68.Value, 3) : null,
                    Channel69 = x.Channel69 != null ? (double?)Math.Round(x.Channel69.Value, 3) : null,
                    Channel70 = x.Channel70 != null ? (double?)Math.Round(x.Channel70.Value, 3) : null,
                    Digital1 = x.Digital1,
                    Digital2 = x.Digital2,
                    Digital3 = x.Digital3,
                    Digital4 = x.Digital4,
                    Digital5 = x.Digital5,
                    Digital6 = x.Digital6,
                    Digital7 = x.Digital7,
                    Digital8 = x.Digital8,
                    Digital9 = x.Digital9,
                    Digital10 = x.Digital10,
                    Digital11 = x.Digital11,
                    Digital12 = x.Digital12,
                    Digital13 = x.Digital13,
                    Digital14 = x.Digital14,
                    Digital15 = x.Digital15,
                    Digital16 = x.Digital16,
                    Digital17 = x.Digital17,
                    Digital18 = x.Digital18,
                    Digital19 = x.Digital19,
                    Digital20 = x.Digital20,
                    Digital21 = x.Digital21,
                    Digital22 = x.Digital22,
                    Digital23 = x.Digital23,
                    Digital24 = x.Digital24,
                    Digital25 = x.Digital25,
                    Digital26 = x.Digital26,
                    Digital27 = x.Digital27,
                    Digital28 = x.Digital28,
                    Digital29 = x.Digital29,
                    Digital30 = x.Digital30,
                    Digital31 = x.Digital31,
                    Digital32 = x.Digital32,
                    Digital33 = x.Digital33,
                    Digital34 = x.Digital34,
                    Digital35 = x.Digital35,
                    Digital36 = x.Digital36,
                    Digital37 = x.Digital37,
                    Digital38 = x.Digital38,
                    Digital39 = x.Digital39,
                    Digital40 = x.Digital40,
                    Digital41 = x.Digital41,
                    Digital42 = x.Digital42,
                    Digital43 = x.Digital43,
                    Digital44 = x.Digital44,
                    Digital45 = x.Digital45,
                    Digital46 = x.Digital46,
                    Digital47 = x.Digital47,
                    Digital48 = x.Digital48,
                    Digital49 = x.Digital49,
                    Digital50 = x.Digital50
                })
                .Take(take)
                .ToListAsync();

            var dtoRows = new WaveDataRowDto[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                dtoRows[i] = new WaveDataRowDto
                {
                    SampleNo = r.SampleNo,
                    TimeRaw = r.TimeRaw,
                    Analog = ReadAnalogs(r, ch),
                    Digital = ReadDigitals(r, dg)
                };
            }

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

        public async Task<(string FilePath, string FileName)> GetFileDownloadInfoAsync(string analysisGuid, CancellationToken ct)
        {
            var a = await _context.ZwavAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid, ct);
            if (a == null) return (null, null);

            var f = await _context.ZwavFiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == a.FileId, ct);
            if (f == null) return (null, null);

            return (f.StoragePath, f.OriginalName);
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
                    // 1. 删除原始上传文件
                    if (!string.IsNullOrWhiteSpace(f.StoragePath) && File.Exists(f.StoragePath))
                    {
                        File.Delete(f.StoragePath);
                    }

                    // 2. 删除解压后的文件夹 (ExtractPath)
                    if (!string.IsNullOrWhiteSpace(f.ExtractPath) && Directory.Exists(f.ExtractPath))
                    {
                        Directory.Delete(f.ExtractPath, recursive: true);
                    }
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