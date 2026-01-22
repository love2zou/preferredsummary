// =========================
// File: Preferred.Api/Controllers/VideoAnalyticsController.cs
// =========================
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Preferred.Api.Models;
using Preferred.Api.Services;
using Video.Application.Dto;

namespace Preferred.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class VideoAnalyticsController : ControllerBase
    {
        private readonly IVideoAnalyticsService _svc;

        private const string DefaultAlgoParamsJson =
        "{"
        + "\"SampleFps\":8,"
        + "\"DiffThreshold\":35,"
        + "\"DiffThresholdMin\":12,"
        + "\"AdaptiveDiffK\":2.2,"
        + "\"MinContourArea\":60,"
        + "\"MeanDeltaRise\":6.0,"
        + "\"MeanDeltaFall\":4.0,"
        + "\"BrightStdK\":2.0,"
        + "\"BrightThrMin\":170,"
        + "\"BrightThrMax\":250,"
        + "\"BrightRatioDelta\":0.0012,"
        + "\"FlashAreaRatio\":0.22,"
        + "\"GlobalBrightnessDelta\":12,"
        + "\"MaxPulseSec\":1.3,"
        + "\"SustainRejectSec\":2.0,"
        + "\"ResizeMaxWidth\":640,"
        + "\"BlurKernel\":5,"
        + "\"RequireConsecutiveHits\":1,"
        + "\"CooldownSec\":1,"
        + "\"MergeGapSec\":2,"
        + "\"MaxMotionRatioPerSec\":0.12"
        + "}";


        public VideoAnalyticsController(IVideoAnalyticsService svc)
        {
            _svc = svc;
        }

        [HttpPost("job")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<CreateJobResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateJob([FromBody] CreateVideoJobRequest req, CancellationToken ct)
        {
            try
            {
                var algo = string.IsNullOrWhiteSpace(req?.AlgoParamsJson) ? DefaultAlgoParamsJson : req.AlgoParamsJson;
                var r = await _svc.CreateJobAsync(algo, ct);

                return Ok(new ApiResponse<CreateJobResultDto>
                {
                    Success = true,
                    Message = "会话已创建",
                    Data = r
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建会话失败", Details = ex.Message });
            }
        }

        [HttpPost("job/{jobNo}/upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(1024L * 1024 * 1024)]
        [ProducesResponseType(typeof(ApiResponse<UploadVideoResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadOne([FromRoute] string jobNo, [FromForm] IFormFile file, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return BadRequest(new ApiErrorResponse { Message = "jobNo 不能为空" });
            if (file == null || file.Length == 0)
                return BadRequest(new ApiErrorResponse { Message = "请选择要上传的视频文件" });

            try
            {
                var r = await _svc.UploadAndEnqueueAsync(jobNo, file, ct);
                return Ok(new ApiResponse<UploadVideoResultDto>
                {
                    Success = true,
                    Message = "上传成功，已入队分析",
                    Data = r
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new ApiErrorResponse { Message = "客户端已取消请求" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = "参数错误", Details = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "上传失败", Details = ex.Message });
            }
        }

        [HttpPost("analyze")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(1024L * 1024 * 1024)]
        [ProducesResponseType(typeof(ApiResponse<CreateJobResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Analyze([FromForm] IFormFile[] files, [FromForm] string algoParamsJson, CancellationToken ct)
        {
            if (files == null || files.Length == 0)
                return BadRequest(new ApiErrorResponse { Message = "请选择要上传的视频文件" });

            if (string.IsNullOrWhiteSpace(algoParamsJson))
                algoParamsJson = DefaultAlgoParamsJson;

            try
            {
                var r = await _svc.CreateAndEnqueueAsync(files, algoParamsJson, ct);

                return Ok(new ApiResponse<CreateJobResultDto>
                {
                    Success = true,
                    Message = "任务已创建并进入队列",
                    Data = r
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建分析任务失败", Details = ex.Message });
            }
        }

        [HttpGet("job/{jobNo}")]
        public async Task<IActionResult> GetJob([FromRoute] string jobNo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return NotFound(new ApiErrorResponse { Message = "任务编号不能为空" });

            try
            {
                var dto = await _svc.GetJobAsync(jobNo, ct);
                if (dto == null)
                    return NotFound(new ApiErrorResponse { Message = "任务不存在" });

                return Ok(new ApiResponse<JobDetailDto> { Success = true, Message = "查询成功", Data = dto });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询任务详情失败", Details = ex.Message });
            }
        }

        [HttpGet("job/{jobNo}/events")]
        [ProducesResponseType(typeof(ApiResponse<List<EventDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetJobEvents([FromRoute] string jobNo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return NotFound(new ApiErrorResponse { Message = "任务编号不能为空" });

            try
            {
                var list = await _svc.GetJobEventsAsync(jobNo, ct);
                return Ok(new ApiResponse<List<EventDto>> { Success = true, Message = "查询成功", Data = list ?? new List<EventDto>() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询事件列表失败", Details = ex.Message });
            }
        }

        [HttpGet("event/{eventId}/snapshots")]
        [ProducesResponseType(typeof(ApiResponse<List<SnapshotDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEventSnapshots([FromRoute] int eventId, CancellationToken ct)
        {
            if (eventId <= 0)
                return BadRequest(new ApiErrorResponse { Message = "eventId 不合法" });

            try
            {
                var list = await _svc.GetEventSnapshotsAsync(eventId, ct);
                return Ok(new ApiResponse<List<SnapshotDto>> { Success = true, Message = "查询成功", Data = list ?? new List<SnapshotDto>() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询事件截图失败", Details = ex.Message });
            }
        }

        [HttpGet("snapshot/{snapshotId}/download")]
        public async Task<IActionResult> DownloadSnapshot([FromRoute] int snapshotId, CancellationToken ct)
        {
            if (snapshotId <= 0)
                return BadRequest(new ApiErrorResponse { Message = "snapshotId 不合法" });

            try
            {
                var path = await _svc.GetSnapshotPathAsync(snapshotId, ct);
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                    return NotFound(new ApiErrorResponse { Message = "截图不存在" });

                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(path, out var contentType))
                    contentType = "application/octet-stream";

                return PhysicalFile(path, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "下载截图失败", Details = ex.Message });
            }
        }

        [HttpGet("video/{fileId}/stream")]
        public async Task<IActionResult> StreamVideo([FromRoute] int fileId, CancellationToken ct)
        {
            if (fileId <= 0)
                return BadRequest(new ApiErrorResponse { Message = "fileId 不合法" });

            try
            {
                var path = await _svc.GetVideoPathAsync(fileId, ct);
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                    return NotFound(new ApiErrorResponse { Message = "视频不存在" });

                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(path, out var contentType))
                    contentType = "application/octet-stream";

                return PhysicalFile(path, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "加载视频失败", Details = ex.Message });
            }
        }

        [HttpDelete("job/{jobNo}")]
        public async Task<IActionResult> DeleteJob([FromRoute] string jobNo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return NotFound(new ApiErrorResponse { Message = "任务编号不能为空" });

            try
            {
                var r = await _svc.DeleteJobAsync(jobNo, ct);
                if (!r.Success)
                    return NotFound(new ApiErrorResponse { Message = r.Message ?? "任务不存在" });

                return Ok(new ApiResponse<object> { Success = true, Message = r.Message ?? "删除成功", Data = null });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除任务失败", Details = ex.Message });
            }
        }

        [HttpPost("job/{jobNo}/close")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CloseJob([FromRoute] string jobNo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return NotFound(new ApiErrorResponse { Message = "任务编号不能为空" });

            try
            {
                var ok = await _svc.CloseJobAsync(jobNo, ct);
                if (!ok)
                    return NotFound(new ApiErrorResponse { Message = "任务不存在或不可关闭" });

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "已关闭上传（后续不再接收新视频）。待所有视频处理完成后将自动置为完成。",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "关闭上传失败", Details = ex.Message });
            }
        }

        [HttpPost("job/{jobNo}/reanalyze")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<ReanalyzeResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reanalyze([FromRoute] string jobNo, [FromBody] ReanalyzeVideoRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobNo))
                return BadRequest(new ApiErrorResponse { Message = "jobNo 不能为空" });

            try
            {
                var fileIds = req?.FileIds ?? Array.Empty<int>();
                var r = await _svc.ReanalyzeFilesAsync(jobNo, fileIds, ct);
                return Ok(new ApiResponse<ReanalyzeResultDto>
                {
                    Success = true,
                    Message = "已重新入队分析",
                    Data = r
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiErrorResponse { Message = "参数错误", Details = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "重新分析失败", Details = ex.Message });
            }
        }
    }
}