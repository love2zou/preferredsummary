using System;
using System.Threading;
using System.Threading.Tasks;
using Zwav.Application.Parsing;
using System.Collections.Generic;
using Zwav.Application.Sag;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 暂降事件应用服务。
    /// 负责录波文件暂降分析的发起、后台处理、结果查询、过程预览以及规则维护。
    /// </summary>
    public interface IZwavSagEventService
    {
        /// <summary>
        /// 分页查询暂降分析结果列表，默认返回每个录波文件最新的一条事件记录。
        /// </summary>
        Task<PagedResult<ZwavSagListItemDto>> QueryAsync(
            string keyword,
            string eventType,
            string phase,
            DateTime? fromUtc,
            DateTime? toUtc,
            int page,
            int pageSize);

        /// <summary>
        /// 发起暂降分析请求，将满足条件的录波文件加入后台分析队列。
        /// </summary>
        Task<AnalyzeZwavSagResponse> AnalyzeAsync(AnalyzeZwavSagRequest req);

        /// <summary>
        /// 后台消费一条暂降分析任务，完成上下文构建、分析执行以及结果落库。
        /// </summary>
        Task ProcessQueuedAnalyzeAsync(ZwavSagAnalysisQueueItem item, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取单个暂降事件详情。
        /// </summary>
        Task<ZwavSagDetailDto> GetDetailAsync(int id);

        /// <summary>
        /// 获取指定暂降事件关联的相别明细。
        /// </summary>
        Task<ZwavSagPhaseDto[]> GetPhasesAsync(int id);

        /// <summary>
        /// 根据录波文件 ID 查询其对应的暂降事件记录。
        /// </summary>
        Task<ZwavSagDetailDto[]> GetByFileIdAsync(int fileId);

        /// <summary>
        /// 更新暂降事件基础信息。
        /// </summary>
        Task<bool> UpdateAsync(int id, UpdateZwavSagEventRequest req);

        /// <summary>
        /// 删除指定暂降事件。
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// 删除某个录波文件下的全部暂降事件及其关联数据。
        /// </summary>
        Task<int> DeleteByFileIdAsync(int fileId);

        /// <summary>
        /// 获取暂降处理过程视图所需的完整上下文。
        /// </summary>
        Task<ZwavSagProcessDto> GetProcessAsync(int id);

        /// <summary>
        /// 基于前端指定参数临时预览暂降处理效果，不修改已持久化结果。
        /// </summary>
        Task<ZwavSagProcessPreviewResponse> PreviewProcessAsync(int id, ZwavSagProcessPreviewRequest req);

        /// <summary>
        /// 分页查询电压通道相别匹配规则。
        /// </summary>
        Task<PagedResult<ZwavSagChannelRuleDto>> QueryChannelRuleAsync(string keyword, int page, int pageSize);

        /// <summary>
        /// 新增电压通道相别匹配规则。
        /// </summary>
        Task<ZwavSagChannelRuleDto> CreateChannelRuleAsync(CreateZwavSagChannelRuleRequest req);

        /// <summary>
        /// 更新电压通道相别匹配规则。
        /// </summary>
        Task<bool> UpdateChannelRuleAsync(int id, UpdateZwavSagChannelRuleRequest req);

        /// <summary>
        /// 删除电压通道相别匹配规则。
        /// </summary>
        Task<bool> DeleteChannelRuleAsync(int id);

        /// <summary>
        /// 分页查询电压通道分组规则。
        /// </summary>
        Task<PagedResult<ZwavSagGroupRuleDto>> QueryGroupRuleAsync(string keyword, int page, int pageSize);

        /// <summary>
        /// 新增电压通道分组规则。
        /// </summary>
        Task<ZwavSagGroupRuleDto> CreateGroupRuleAsync(CreateZwavSagGroupRuleRequest req);

        /// <summary>
        /// 更新电压通道分组规则。
        /// </summary>
        Task<bool> UpdateGroupRuleAsync(int id, UpdateZwavSagGroupRuleRequest req);

        /// <summary>
        /// 删除电压通道分组规则。
        /// </summary>
        Task<bool> DeleteGroupRuleAsync(int id);
    }
}
