using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Video.Application.Dto;

namespace Preferred.Api.Services
{
    public interface IVideoAnalyticsService
    {
        // 旧接口（可保留兼容批量）
        Task<CreateJobResultDto> CreateAndEnqueueAsync(IFormFile[] files, string algoParamsJson, CancellationToken ct);

        Task<JobDetailDto> GetJobAsync(string jobNo, CancellationToken ct);
        Task<List<EventDto>> GetJobEventsAsync(string jobNo, CancellationToken ct);
        Task<List<SnapshotDto>> GetEventSnapshotsAsync(int eventId, CancellationToken ct);

        Task<string> GetSnapshotPathAsync(int snapshotId, CancellationToken ct);
        Task<string> GetVideoPathAsync(int fileId, CancellationToken ct);

        Task<bool> CancelJobAsync(string jobNo, CancellationToken ct);
        Task<DeleteJobResultDto> DeleteJobAsync(string jobNo, CancellationToken ct);

        // 新增：持续会话 + 单视频收到即入队
        Task<CreateJobResultDto> CreateJobAsync(string algoParamsJson, CancellationToken ct);
        Task<bool> CloseJobAsync(string jobNo, CancellationToken ct);
        Task<UploadVideoResultDto> UploadAndEnqueueAsync(string jobNo, IFormFile file, CancellationToken ct);

        Task<ReanalyzeResultDto> ReanalyzeFilesAsync(string jobNo, int[] fileIds, CancellationToken ct);

    }
}
