using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Video.Application.Dto;

namespace Preferred.Api.Services
{
    public interface IVideoAnalyticsService
    {
        Task<CreateJobResultDto> CreateJobAsync(string algoParamsJson);
        Task<UploadVideoResultDto> UploadAndEnqueueAsync(string jobNo, IFormFile file);
        Task<CreateJobResultDto> CreateAndEnqueueAsync(IFormFile[] files, string algoParamsJson);

        Task<JobDetailDto> GetJobAsync(string jobNo);
        Task<List<EventDto>> GetJobEventsAsync(string jobNo);
        Task<List<SnapshotDto>> GetEventSnapshotsAsync(int eventId);
        Task<string> GetSnapshotPathAsync(int snapshotId);
        Task<string> GetVideoPathAsync(int fileId);

        Task<bool> CloseJobAsync(string jobNo);
        Task<ReanalyzeResultDto> ReanalyzeFilesAsync(string jobNo, int[] fileIds, string algoParamsJson);
        Task<DeleteJobResultDto> DeleteJobAsync(string jobNo);
        Task<bool> CancelJobAsync(string jobNo);
    }
}
