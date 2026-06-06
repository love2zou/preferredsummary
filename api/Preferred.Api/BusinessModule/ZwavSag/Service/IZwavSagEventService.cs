using System;
using System.Threading;
using System.Threading.Tasks;
using Preferred.Api.Models;
using Zwav.Application.Parsing;
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

        Task<ZwavSagTaskDto> GetActiveTaskAsync();

        Task<PagedResult<ZwavSagTaskDto>> QueryTasksAsync(string keyword, int? status, bool? isClosed, int page, int pageSize);

        Task<ZwavSagTaskDto> GetTaskAsync(int id);

        Task<PagedResult<ZwavSagTaskFileItemDto>> QueryTaskFilesAsync(int taskId, string keyword, int? status, int page, int pageSize);

        Task<ZwavSagTaskDto> UpdateTaskAsync(int id, UpdateZwavSagTaskRequest req);

        Task<ZwavSagTaskDto> CloseTaskAsync(int id);

        Task<bool> DeleteTaskAsync(int id);

        Task ProcessQueuedAnalyzeAsync(ZwavSagAnalysisQueueItem item, CancellationToken cancellationToken = default);

        Task<ZwavSagDetailDto> GetDetailAsync(int id);

        Task<ZwavSagPhaseDto[]> GetPhasesAsync(int id);

        Task<ZwavSagDetailDto[]> GetByFileIdAsync(int fileId);

        Task<bool> DeleteAsync(int id);

        Task<int> DeleteByFileIdAsync(int fileId);

        Task<ZwavSagProcessDto> GetProcessAsync(int id);

        Task<ZwavSagProcessPreviewResponse> PreviewProcessAsync(int id, ZwavSagProcessPreviewRequest req);

        Task<PagedResult<ZwavSagChannelRuleDto>> QueryChannelRuleAsync(string keyword, bool? enabled, int page, int pageSize);

        Task<ZwavSagChannelRuleDto> CreateChannelRuleAsync(CreateZwavSagChannelRuleRequest req);

        Task<bool> UpdateChannelRuleAsync(int id, UpdateZwavSagChannelRuleRequest req);

        Task<bool> DeleteChannelRuleAsync(int id);

        Task<PagedResult<ZwavSagGroupRuleDto>> QueryGroupRuleAsync(string keyword, int page, int pageSize);

        Task<ZwavSagGroupRuleDto> CreateGroupRuleAsync(CreateZwavSagGroupRuleRequest req);

        Task<bool> UpdateGroupRuleAsync(int id, UpdateZwavSagGroupRuleRequest req);

        Task<bool> DeleteGroupRuleAsync(int id);
    }
}
