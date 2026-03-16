using System;
using System.Threading.Tasks;
using Zwav.Application.Parsing;
using System.Collections.Generic;
using Zwav.Application.Sag;
using Zwav.Application.Parsing;

namespace Preferred.Api.Services
{
   public interface IZwavSagEventService
    {
        Task<PagedResult<ZwavSagListItemDto>> QueryAsync(
            string keyword,
            string eventType,
            string phase,
            DateTime? fromUtc,
            DateTime? toUtc,
            int page,
            int pageSize);

        Task<AnalyzeZwavSagResponse> AnalyzeAsync(AnalyzeZwavSagRequest req);

        Task<ZwavSagDetailDto> GetDetailAsync(int id);
        Task<ZwavSagPhaseDto[]> GetPhasesAsync(int id);
        Task<ZwavSagDetailDto[]> GetByAnalysisIdAsync(int analysisId);

        Task<bool> UpdateAsync(int id, UpdateZwavSagEventRequest req);
        Task<bool> DeleteAsync(int id);
        Task<int> DeleteByAnalysisIdAsync(int analysisId);
    }
}