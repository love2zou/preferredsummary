using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

        /// <summary>
        /// 上传并创建解析任务：写 Tb_ZwavFile + Tb_ZwavAnalysis，然后入队后台解析
        /// </summary>
        public async Task<(string AnalysisGuid, string Status)> CreateAnalysisAsync(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                throw new InvalidOperationException("file is required");

            var guid = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            // 1) 落盘 + hash（你原来的逻辑保留）:contentReference[oaicite:5]{index=5}
            var saved = await _storage.SaveAsync(file, guid);

            // 2) 写 Tb_ZwavFile（风格参考 TagService/UserService：直接 _context.Add + SaveChanges）:contentReference[oaicite:6]{index=6}:contentReference[oaicite:7]{index=7}
            var zwavFile = new ZwavFile
            {
                OriginalName = file.FileName,
                FileSize = (int)saved.SizeBytes,  // 你表是 INT；若后续改 BIGINT，这里改 long
                Sha256 = saved.Sha256,
                StorageType = "Local",
                StoragePath = saved.FullPath,
                ExtractPath = null,
                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavFiles.Add(zwavFile);
            await _context.SaveChangesAsync();

            // 3) 写 Tb_ZwavAnalysis
            var analysis = new ZwavAnalysis
            {
                AnalysisGuid = guid,
                Status = "Queued",
                Progress = 0,
                ErrorMessage = null,

                FileId = zwavFile.Id,
                TotalRecords = null,
                RecordSize = null,
                DigitalWords = null,

                StartTime = null,
                FinishTime = null,

                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            };

            _context.ZwavAnalyses.Add(analysis);
            await _context.SaveChangesAsync();

            // 4) 入队后台解析（你原来的逻辑保留）:contentReference[oaicite:8]{index=8}
            await _queue.EnqueueAsync(guid);

            return (guid, "Queued");
        }

        /// <summary>
        /// 查询任务状态（直接查 Tb_ZwavAnalysis）
        /// </summary>
        public async Task<AnalysisStatusResponse> GetStatusAsync(string analysisGuid)
        {
            var a = await _context.ZwavAnalyses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AnalysisGuid == analysisGuid);

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

        public Task<AnalysisMetaResponse> GetMetaAsync(string analysisGuid)
            => throw new NotImplementedException();

        public Task<SamplesResponse> GetSamplesAsync(string analysisGuid, long start, int count, string[] channels)
            => throw new NotImplementedException();

        public Task<WaveResponse> GetWaveAsync(string analysisGuid, long start, long end, string[] channels, int maxPoints, string mode)
            => throw new NotImplementedException();

        public Task<(byte[] Content, string FileName)?> ExportCsvAsync(string analysisGuid, long start, long end, string[] channels)
            => throw new NotImplementedException();

        public Task<bool> DeleteAsync(string analysisGuid)
            => throw new NotImplementedException();
    }
}