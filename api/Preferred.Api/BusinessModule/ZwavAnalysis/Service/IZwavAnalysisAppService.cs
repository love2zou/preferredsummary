using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IZwavAnalysisAppService
    {
        Task<UploadZwavFileResult> UploadAsync(IFormFile file, CancellationToken ct);
        /// <summary>
        /// 基于已上传文件(FileId)创建解析任务：读 Tb_ZwavFile -> 写 Tb_ZwavAnalysis -> 入队后台解析
        /// </summary>
        Task<(string AnalysisGuid, string Status)> CreateAnalysisByFileIdAsync(
            int fileId,
            bool forceRecreate,
            CancellationToken ct);

        Task<AnalysisStatusResponse> GetStatusAsync(string analysisGuid, CancellationToken ct);

        Task<PagedResult<AnalysisListItemDto>> QueryAsync(
            string status, string keyword, DateTime? fromUtc, DateTime? toUtc,
            int pageIndex, int pageSize, string orderBy, CancellationToken ct);

        Task<AnalysisDetailDto> GetDetailAsync(string analysisGuid, CancellationToken ct);

        Task<CfgDto> GetCfgAsync(string analysisGuid, bool includeText, CancellationToken ct);

        Task<ChannelDto[]> GetChannelsAsync(string analysisGuid, string type, bool enabledOnly, CancellationToken ct);

        Task<HdrDto> GetHdrAsync(string analysisGuid, CancellationToken ct);

        Task<WaveDataPageDto> GetWaveDataAsync(
            string analysisGuid,
            int? fromSample, int? toSample,
            int? offset, int? limit,
            string channels, string digitals,
            int downSample,
            CancellationToken ct);

        Task<bool> DeleteAsync(string analysisGuid, bool deleteFile, CancellationToken ct);
    }
}