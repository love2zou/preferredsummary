using System;
using System.Threading.Tasks;
using Zwav.Application.Parsing;
using System.Collections.Generic;
using Zwav.Application.Sag;

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
        Task<ZwavSagDetailDto[]> GetByFileIdAsync(int fileId);

        Task<bool> UpdateAsync(int id, UpdateZwavSagEventRequest req);
        Task<bool> DeleteAsync(int id);
        Task<int> DeleteByFileIdAsync(int fileId);

        Task<ZwavSagProcessDto> GetProcessAsync(int id);
        Task<ZwavSagProcessPreviewResponse> PreviewProcessAsync(int id, ZwavSagProcessPreviewRequest req);

        Task<PagedResult<ZwavSagChannelRuleDto>> QueryChannelRuleAsync(string keyword, int page, int pageSize);
        Task<ZwavSagChannelRuleDto> CreateChannelRuleAsync(CreateZwavSagChannelRuleRequest req);
        Task<bool> UpdateChannelRuleAsync(int id, UpdateZwavSagChannelRuleRequest req);
        Task<bool> DeleteChannelRuleAsync(int id);

        Task<PagedResult<ZwavSagGroupRuleDto>> QueryGroupRuleAsync(string keyword, int page, int pageSize);
        Task<ZwavSagGroupRuleDto> CreateGroupRuleAsync(CreateZwavSagGroupRuleRequest req);
        Task<bool> UpdateGroupRuleAsync(int id, UpdateZwavSagGroupRuleRequest req);
        Task<bool> DeleteGroupRuleAsync(int id);
    }
}
