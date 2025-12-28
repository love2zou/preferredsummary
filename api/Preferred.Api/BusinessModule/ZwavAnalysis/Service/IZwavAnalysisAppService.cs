using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Preferred.Api.Models;
using System.IO;
using System;

namespace Preferred.Api.Services
{
    public interface IZwavAnalysisAppService
    {
        Task<(string AnalysisGuid, string Status)> CreateAnalysisAsync(IFormFile file);

        Task<AnalysisStatusResponse> GetStatusAsync(string analysisGuid);

        Task<AnalysisMetaResponse> GetMetaAsync(string analysisGuid);

        Task<SamplesResponse> GetSamplesAsync(string analysisGuid, long start, int count, string[] channels);

        Task<WaveResponse> GetWaveAsync(string analysisGuid, long start, long end, string[] channels, int maxPoints, string mode);

        Task<(byte[] Content, string FileName)?> ExportCsvAsync(string analysisGuid, long start, long end, string[] channels);

        Task<bool> DeleteAsync(string analysisGuid);
    }
}